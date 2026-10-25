using Microsoft.AspNetCore.Http.HttpResults;
using RouteFinder.Features.Route.Models;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route.Endpoints;

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        var routes = app.MapGroup("/api/routes").WithTags("Routes");

        routes.MapPost("/generate", GenerateRouteAsync)
            .WithSummary("Generate a round trip that starts and ends at the given point.")
            .RequireRateLimiting(RouteRateLimiting.GeneratePolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    private static async Task<Ok<RouteResponse>> GenerateRouteAsync(
        RouteRequest request,
        IOpenRouteService routeService,
        CancellationToken cancellationToken)
    {
        // The validation filter has already rejected requests with missing values.
        var route = await routeService.GenerateRoundTripAsync(
            request.Latitude!.Value,
            request.Longitude!.Value,
            request.Distance!.Value,
            request.Profile,
            cancellationToken);

        return TypedResults.Ok(route);
    }
}
