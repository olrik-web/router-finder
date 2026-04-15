using RouteFinder.Features.Route.Models;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Tests.Infrastructure;

public sealed class TestOpenRouteService : IOpenRouteService
{
    public Func<double, double, double, string, CancellationToken, Task<RouteResponse>> GenerateRouteAsync { get; set; } =
        (_, _, distanceKm, _, _) => Task.FromResult(CreateRouteResponse(distanceKm));

    public Func<double, double, double, string, string, int, CancellationToken, Task<(byte[] Content, string ContentType)>> DownloadRouteAsync { get; set; } =
        (_, _, _, _, _, _, _) => Task.FromResult<(byte[] Content, string ContentType)>(([1, 2, 3], "application/octet-stream"));

    public Task<RouteResponse> GenerateCircularRoute(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default)
    {
        return GenerateRouteAsync(lat, lon, distanceKm, profile, cancellationToken);
    }

    public Task<(byte[] Content, string ContentType)> DownloadRouteFileAsync(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        string format,
        int seed,
        CancellationToken cancellationToken = default)
    {
        return DownloadRouteAsync(lat, lon, distanceKm, profile, format, seed, cancellationToken);
    }

    public static RouteResponse CreateRouteResponse(
        double distanceKm = 5,
        double durationSeconds = 1800,
        int seed = 1234)
    {
        return new RouteResponse(
            [
                new Coordinate(56.1572, 10.2107),
                new Coordinate(56.1672, 10.2207)
            ],
            distanceKm,
            durationSeconds,
            $"{distanceKm:F1}km route",
            seed);
    }
}