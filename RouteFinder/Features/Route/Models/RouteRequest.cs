namespace RouteFinder.Features.Route.Models;

public record RouteRequest(
    double Latitude,
    double Longitude,
    double Distance,
    string Profile = RouteValidation.DefaultProfile
);