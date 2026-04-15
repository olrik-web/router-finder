using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route;

internal static class RouteValidation
{
    public const string DefaultProfile = "cycling-regular";
    public const double MinDistanceKm = 1;
    public const double MaxDistanceKm = 50;

    private static readonly HashSet<string> SupportedProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "foot-walking",
        "foot-hiking",
        "cycling-regular",
        "cycling-road",
        "cycling-mountain",
        "cycling-electric",
        "driving-car"
    };

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "json",
        "geojson",
        "gpx"
    };

    public static Dictionary<string, string[]> Validate(RouteRequest request)
    {
        return ValidateRouteRequest(request.Latitude, request.Longitude, request.Distance, request.Profile);
    }

    public static Dictionary<string, string[]> Validate(DownloadRouteRequest request, string? format)
    {
        var errors = ValidateRouteRequest(request.Latitude, request.Longitude, request.Distance, request.Profile);

        if (string.IsNullOrWhiteSpace(format))
        {
            errors["format"] = ["Format is required."];
            return errors;
        }

        if (!IsSupportedFormat(format))
        {
            errors["format"] = [$"Format must be one of: {string.Join(", ", SupportedFormats)}."];
        }

        return errors;
    }

    public static bool IsSupportedProfile(string? profile)
    {
        return !string.IsNullOrWhiteSpace(profile) && SupportedProfiles.Contains(profile);
    }

    public static bool IsSupportedFormat(string? format)
    {
        return !string.IsNullOrWhiteSpace(format) && SupportedFormats.Contains(format);
    }

    public static string NormalizeProfile(string profile)
    {
        return profile.Trim().ToLowerInvariant();
    }

    public static string NormalizeFormat(string format)
    {
        return format.Trim().ToLowerInvariant();
    }

    private static Dictionary<string, string[]> ValidateRouteRequest(
        double latitude,
        double longitude,
        double distance,
        string? profile)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude is < -90 or > 90)
        {
            errors["latitude"] = ["Latitude must be between -90 and 90."];
        }

        if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude is < -180 or > 180)
        {
            errors["longitude"] = ["Longitude must be between -180 and 180."];
        }

        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < MinDistanceKm || distance > MaxDistanceKm)
        {
            errors["distance"] = [$"Distance must be between {MinDistanceKm} and {MaxDistanceKm} kilometers."];
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            errors["profile"] = ["Profile is required."];
        }
        else if (!IsSupportedProfile(profile))
        {
            errors["profile"] = [$"Profile must be one of: {string.Join(", ", SupportedProfiles)}."];
        }

        return errors;
    }
}