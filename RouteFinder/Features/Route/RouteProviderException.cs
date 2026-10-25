namespace RouteFinder.Features.Route;

public enum RouteProviderError
{
    /// <summary>The provider could not be reached or answered with an error.</summary>
    Unavailable,

    /// <summary>The provider did not answer within the configured timeout.</summary>
    Timeout,

    /// <summary>The provider answered, but the body could not be used as a route.</summary>
    InvalidResponse,

    /// <summary>There is no road or path near the start point, so no route exists.</summary>
    NoRoutablePoint
}

public sealed class RouteProviderException : Exception
{
    public RouteProviderException(RouteProviderError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public RouteProviderError Error { get; }
}
