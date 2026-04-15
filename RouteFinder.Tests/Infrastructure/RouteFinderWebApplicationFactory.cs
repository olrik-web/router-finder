using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Tests.Infrastructure;

public sealed class RouteFinderWebApplicationFactory(TestOpenRouteService routeService) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouteService:ApiKey"] = "test-api-key"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenRouteService>();
            services.AddSingleton<IOpenRouteService>(routeService);
        });
    }
}