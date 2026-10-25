namespace RouteFinder.Features.Route.Models;

// Shapes of the OpenRouteService GeoJSON directions response. Only the fields we read are mapped.
// Everything is nullable because the provider can send null for any of it, and an unchecked null
// would surface as a 500 instead of an invalid-response error.

public class RouteResponseRoot
{
    public string? Type { get; set; }
    public List<RouteFeature?>? Features { get; set; }
}

public class RouteFeature
{
    public string? Type { get; set; }
    public RouteGeometry? Geometry { get; set; }
    public RouteProperties? Properties { get; set; }
}

public class RouteGeometry
{
    public string? Type { get; set; }
    public List<List<double>?>? Coordinates { get; set; }
}

public class RouteProperties
{
    public RouteSummary? Summary { get; set; }
    public List<RouteSegment?>? Segments { get; set; }
}

public class RouteSummary
{
    public double Distance { get; set; }
    public double Duration { get; set; }
}

public class RouteSegment
{
    public double Distance { get; set; }
    public double Duration { get; set; }
}
