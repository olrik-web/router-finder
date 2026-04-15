using System.ComponentModel.DataAnnotations;
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
        services.AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection("OpenRouteService"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddHttpClient<IOpenRouteService, OpenRouteService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.ApiKey);
        });
        
        return services;
    }
}