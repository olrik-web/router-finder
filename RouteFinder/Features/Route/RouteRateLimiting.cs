using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace RouteFinder.Features.Route;

public static class RouteRateLimiting
{
    public const string GeneratePolicy = "generate-route";

    public static IServiceCollection AddRouteRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RouteRateLimitingOptions>()
            .Bind(configuration.GetSection(RouteRateLimitingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RouteRateLimitingOptions>, RouteRateLimitingOptionsValidator>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(GeneratePolicy, httpContext =>
            {
                var limit = GetOptions(httpContext).Generate;

                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit.PermitLimit,
                        Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : GetOptions(httpContext).Generate.WindowSeconds;

        httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Too many routes requested. Try again in {retryAfterSeconds} seconds.",
                Extensions = { ["retryAfterSeconds"] = retryAfterSeconds }
            }
        });
    }

    private static RouteRateLimitingOptions GetOptions(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOptions<RouteRateLimitingOptions>>().Value;
}
