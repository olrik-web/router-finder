using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route.Services;

public interface IOpenRouteService
{
    /// <exception cref="RouteProviderException">The provider failed or returned no usable route.</exception>
    Task<RouteResponse> GenerateRoundTripAsync(
        double latitude,
        double longitude,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default);
}
