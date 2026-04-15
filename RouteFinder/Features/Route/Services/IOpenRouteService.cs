using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route.Services;

public interface IOpenRouteService
{
    Task<RouteResponse> GenerateCircularRoute(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType)> DownloadRouteFileAsync(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        string format,
        int seed,
        CancellationToken cancellationToken = default);
}