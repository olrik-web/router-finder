namespace RouteFinder.Features.Route.Models;

/// <param name="Coordinates">The route as latitude/longitude pairs, starting and ending at the start point.</param>
/// <param name="Distance">Route length in kilometres.</param>
/// <param name="Duration">Estimated travel time in seconds for the chosen profile.</param>
public record RouteResponse(
    IReadOnlyList<Coordinate> Coordinates,
    double Distance,
    double Duration
);
