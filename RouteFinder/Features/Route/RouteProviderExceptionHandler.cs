using Microsoft.AspNetCore.Diagnostics;

namespace RouteFinder.Features.Route;

internal sealed partial class RouteProviderExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<RouteProviderExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RouteProviderException routeException)
        {
            return false;
        }

        var (statusCode, detail) = routeException.Error switch
        {
            RouteProviderError.NoRoutablePoint => (
                StatusCodes.Status422UnprocessableEntity,
                "There is no road or path near this point. Pick a spot closer to a street or trail."),
            RouteProviderError.Timeout => (
                StatusCodes.Status504GatewayTimeout,
                "The routing service took too long to answer. Try again."),
            RouteProviderError.InvalidResponse => (
                StatusCodes.Status502BadGateway,
                "The routing service sent back a route that could not be read. Try again."),
            _ => (
                StatusCodes.Status502BadGateway,
                "The routing service is not available right now. Try again in a moment.")
        };

        LogRouteProviderFailure(logger, routeException.Error, routeException);

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Detail = detail
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route provider failed with {Error}.")]
    private static partial void LogRouteProviderFailure(ILogger logger, RouteProviderError error, Exception exception);
}
