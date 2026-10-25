using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;

namespace RouteFinder.Features.Route;

/// <summary>OpenRouteService profile names. Constants so they can be used in attributes.</summary>
public static class RouteProfiles
{
    public const string FootWalking = "foot-walking";
    public const string FootHiking = "foot-hiking";
    public const string CyclingRegular = "cycling-regular";
    public const string CyclingRoad = "cycling-road";
    public const string CyclingMountain = "cycling-mountain";
    public const string CyclingElectric = "cycling-electric";
    public const string DrivingCar = "driving-car";

    public const string ValidationMessage =
        "Profile must be one of: " +
        FootWalking + ", " + FootHiking + ", " + CyclingRegular + ", " + CyclingRoad + ", " +
        CyclingMountain + ", " + CyclingElectric + ", " + DrivingCar + ".";

    // Matching ignores case and surrounding spaces, so "Cycling-Regular " is accepted.
    private static readonly FrozenSet<string> Supported = new[]
    {
        FootWalking, FootHiking, CyclingRegular, CyclingRoad, CyclingMountain, CyclingElectric, DrivingCar
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string? profile) =>
        profile is not null && Supported.Contains(profile.Trim());

    /// <summary>Returns the profile name exactly as OpenRouteService expects it. The profile must be supported.</summary>
    public static string Normalize(string profile) =>
        Supported.TryGetValue(profile.Trim(), out var canonical)
            ? canonical
            : throw new ArgumentException($"Unsupported profile '{profile}'.", nameof(profile));
}

/// <summary>Accepts any <see cref="RouteProfiles"/> name, ignoring case and surrounding spaces.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class RouteProfileAttribute : ValidationAttribute
{
    public RouteProfileAttribute()
        : base(RouteProfiles.ValidationMessage)
    {
    }

    public override bool IsValid(object? value) => value is string profile && RouteProfiles.IsSupported(profile);
}
