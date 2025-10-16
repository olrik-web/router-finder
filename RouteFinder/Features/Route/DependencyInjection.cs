using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route;

public class OpenRouteServiceOptions
{
    [Required]
    public required string ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";
}

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection("OpenRouteService"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddHttpClient<OpenRouteService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
        });
        
        return services;
    }
}