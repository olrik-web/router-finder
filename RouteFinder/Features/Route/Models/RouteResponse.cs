namespace RouteFinder.Features.Route.Models;

public record RouteResponse(
    List<Coordinate> Coordinates,
    double Distance,
    double Duration,
    string Summary,
    int Seed
);