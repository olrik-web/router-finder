using RouteFinder.Features.Route.Models;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Tests.Infrastructure;

public sealed class TestOpenRouteService : IOpenRouteService
{
    public Func<double, double, double, string, CancellationToken, Task<RouteResponse>> GenerateRoute { get; set; } =
        (_, _, distanceKm, _, _) => Task.FromResult(CreateRouteResponse(distanceKm));

    public Task<RouteResponse> GenerateRoundTripAsync(
        double latitude,
        double longitude,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default)
    {
        return GenerateRoute(latitude, longitude, distanceKm, profile, cancellationToken);
    }

    public static RouteResponse CreateRouteResponse(double distanceKm = 5, double durationSeconds = 1800)
    {
        return new RouteResponse(
            [
                new Coordinate(56.1572, 10.2107),
                new Coordinate(56.1672, 10.2207)
            ],
            distanceKm,
            durationSeconds);
    }
}
