namespace RouteFinder.Features.Route.Models;

public record DownloadRouteRequest(
    double Latitude,
    double Longitude,
    double Distance,
    int Seed,
    string Profile = RouteValidation.DefaultProfile
);