using Microsoft.Extensions.Options;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route;

public static class RouteFeature
{
    public static IServiceCollection AddRouteFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection(OpenRouteServiceOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpenRouteServiceOptions>, OpenRouteServiceOptionsValidator>();

        services.AddHttpClient<IOpenRouteService, OpenRouteService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.ApiKey);
        });

        services.AddExceptionHandler<RouteProviderExceptionHandler>();
        services.AddRouteRateLimiting(configuration);

        return services;
    }
}
