namespace RouteFinder.Features.Route;

/// <summary>OpenRouteService profile names. Constants so they can be used in validation attributes.</summary>
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
}
