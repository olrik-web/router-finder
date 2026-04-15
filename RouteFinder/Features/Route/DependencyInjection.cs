using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route;

public class OpenRouteServiceOptions
{
    [Required]
    public required string ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 15;
}

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitOptions = configuration.GetSection(RouteRateLimitingOptions.SectionName).Get<RouteRateLimitingOptions>() ?? new RouteRateLimitingOptions();

        services.AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection("OpenRouteService"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RouteRateLimitingOptions>()
            .Bind(configuration.GetSection(RouteRateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                var retryAfterSeconds = GetRetryAfterSeconds(context, rateLimitOptions);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                var problem = new ProblemDetails
                {
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    Detail = GetRateLimitDetail(context.HttpContext.Request.Path)
                };

                problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: token);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var partitionKey = GetIpPartitionKey(httpContext);
                var path = httpContext.Request.Path;

                if (path.StartsWithSegments("/api/routes/generate", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{partitionKey}:generate",
                        _ => CreateLimiterOptions(rateLimitOptions.Generate));
                }

                if (path.StartsWithSegments("/api/routes/download", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{partitionKey}:download",
                        _ => CreateLimiterOptions(rateLimitOptions.Download));
                }

                return RateLimitPartition.GetNoLimiter("routefinder-unlimited");
            });
        });
        
        services.AddHttpClient<IOpenRouteService, OpenRouteService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.ApiKey);
        });
        
        return services;
    }

    private static FixedWindowRateLimiterOptions CreateLimiterOptions(RouteRateLimitPolicyOptions options)
    {
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.PermitLimit,
            Window = TimeSpan.FromSeconds(options.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        };
    }

    private static string GetIpPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static int GetRetryAfterSeconds(OnRejectedContext context, RouteRateLimitingOptions options)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        return context.HttpContext.Request.Path.StartsWithSegments("/api/routes/download", StringComparison.OrdinalIgnoreCase)
            ? options.Download.WindowSeconds
            : options.Generate.WindowSeconds;
    }

    private static string GetRateLimitDetail(PathString path)
    {
        return path.StartsWithSegments("/api/routes/download", StringComparison.OrdinalIgnoreCase)
            ? "Too many route download requests from this IP. Please wait before trying again."
            : "Too many route generation requests from this IP. Please wait before trying again.";
    }
}