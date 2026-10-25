using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Tests.Infrastructure;

public sealed class RouteFinderWebApplicationFactory(
    TestOpenRouteService? routeService = null,
    IReadOnlyDictionary<string, string?>? configurationOverrides = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configurationValues = new Dictionary<string, string?>
            {
                ["OpenRouteService:ApiKey"] = "test-api-key"
            };

            foreach (var (key, value) in configurationOverrides ?? new Dictionary<string, string?>())
            {
                configurationValues[key] = value;
            }

            configurationBuilder.AddInMemoryCollection(configurationValues);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenRouteService>();
            services.AddSingleton<IOpenRouteService>(routeService ?? new TestOpenRouteService());
        });
    }
}
