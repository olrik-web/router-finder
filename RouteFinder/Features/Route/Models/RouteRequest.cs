using System.ComponentModel.DataAnnotations;

namespace RouteFinder.Features.Route.Models;

public record RouteRequest(
    [Required(ErrorMessage = "Latitude is required.")]
    [Range(-90d, 90d, ErrorMessage = "Latitude must be between -90 and 90.")]
    double? Latitude,

    [Required(ErrorMessage = "Longitude is required.")]
    [Range(-180d, 180d, ErrorMessage = "Longitude must be between -180 and 180.")]
    double? Longitude,

    [Required(ErrorMessage = "Distance is required.")]
    [Range(RouteRequest.MinDistanceKm, RouteRequest.MaxDistanceKm, ErrorMessage = "Distance must be between {1} and {2} km.")]
    double? Distance,

    [RouteProfile]
    string Profile = RouteProfiles.FootWalking)
{
    public const double MinDistanceKm = 1;
    public const double MaxDistanceKm = 50;
}
