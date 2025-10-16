namespace RouteFinder.Features.Route.Models;

public record DownloadRouteRequest(
    double Latitude,
    double Longitude,
    double Distance, // in kilometers
    int Seed,
    string Profile = "cycling-regular" // cycling-regular, cycling-road, foot-walking
);