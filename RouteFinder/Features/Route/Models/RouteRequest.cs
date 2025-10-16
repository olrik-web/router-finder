namespace RouteFinder.Features.Route.Models;

public record RouteRequest(
    double Latitude,
    double Longitude,
    double Distance, // in kilometers
    string Profile = "cycling-regular" // cycling-regular, cycling-road, foot-walking
);