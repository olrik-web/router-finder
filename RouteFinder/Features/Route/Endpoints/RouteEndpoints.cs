using RouteFinder.Features.Route.Models;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route.Endpoints;

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

        app.MapPost("/api/routes/generate", async (
            RouteRequest request,
            OpenRouteService routeService) =>
        {
            try
            {
                var route = await routeService.GenerateCircularRoute(
                    request.Latitude,
                    request.Longitude,
                    request.Distance,
                    request.Profile
                );
                return Results.Ok(route);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });
        
        app.MapPost("/api/routes/download", async (
            DownloadRouteRequest request,
            string format,
            OpenRouteService routeService) =>
        {
            try
            {
                var result = await routeService.DownloadRouteFileAsync(
                    request.Latitude,
                    request.Longitude,
                    request.Distance,
                    request.Profile,
                    format,
                    request.Seed
                );

                return Results.File(
                    result.Content,
                    result.ContentType,
                    fileDownloadName: $"route.{format}"
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        return app;
    }
}