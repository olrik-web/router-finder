namespace RouteFinder.Features.Route.Models;

public class RouteResponseRoot
{
    public string Type { get; set; } = "";
    public List<RouteFeature> Features { get; set; } = new();
}

public class RouteFeature
{
    public string Type { get; set; } = "";
    public RouteGeometry Geometry { get; set; } = new();
    public RouteProperties Properties { get; set; } = new();
}

public class RouteGeometry
{
    public string Type { get; set; } = "";
    public List<List<double>> Coordinates { get; set; } = new();
}

public class RouteProperties
{
    public RouteSummary Summary { get; set; } = new();
    public List<RouteSegment> Segments { get; set; } = new();
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
