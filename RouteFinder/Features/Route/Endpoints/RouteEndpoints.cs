using System.Text.Json;
using RouteFinder.Features.Route.Models;
using RouteFinder.Features.Route.Services;

namespace RouteFinder.Features.Route.Endpoints;

public static class RouteEndpoints
{
    private sealed class LoggerCategory;

    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

        app.MapPost("/api/routes/generate", async (
            RouteRequest request,
            OpenRouteService routeService,
            ILogger<LoggerCategory> logger,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = RouteValidation.Validate(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            try
            {
                var route = await routeService.GenerateCircularRoute(
                    request.Latitude,
                    request.Longitude,
                    request.Distance,
                    request.Profile,
                    cancellationToken
                );

                return Results.Ok(route);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "Route generation failed with upstream status code {StatusCode} for profile {Profile}.",
                    ex.StatusCode,
                    request.Profile);

                return Results.Problem(
                    detail: "Unable to generate a route from the routing provider right now.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Route generation timed out for profile {Profile}.", request.Profile);

                return Results.Problem(
                    detail: "Route generation timed out. Please try again.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Route generation returned invalid JSON for profile {Profile}.", request.Profile);

                return Results.Problem(
                    detail: "The routing provider returned an unreadable response.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Route generation returned an invalid route payload for profile {Profile}.", request.Profile);

                return Results.Problem(
                    detail: "The routing provider returned an unexpected route response.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while generating route for profile {Profile}.", request.Profile);

                return Results.Problem(
                    detail: "An unexpected error occurred while generating the route.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
        
        app.MapPost("/api/routes/download", async (
            DownloadRouteRequest request,
            string? format,
            OpenRouteService routeService,
            ILogger<LoggerCategory> logger,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = RouteValidation.Validate(request, format);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            try
            {
                var result = await routeService.DownloadRouteFileAsync(
                    request.Latitude,
                    request.Longitude,
                    request.Distance,
                    request.Profile,
                    format!,
                    request.Seed,
                    cancellationToken
                );

                return Results.File(
                    result.Content,
                    result.ContentType,
                    fileDownloadName: $"route.{RouteValidation.NormalizeFormat(format!)}"
                );
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "Route download failed with upstream status code {StatusCode} for profile {Profile} and format {Format}.",
                    ex.StatusCode,
                    request.Profile,
                    format);

                return Results.Problem(
                    detail: "Unable to download a route from the routing provider right now.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Route download timed out for profile {Profile} and format {Format}.", request.Profile, format);

                return Results.Problem(
                    detail: "Route download timed out. Please try again.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Route download validation failed for format {Format}.", format);

                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["format"] = [ex.Message]
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while downloading route for profile {Profile} and format {Format}.", request.Profile, format);

                return Results.Problem(
                    detail: "An unexpected error occurred while downloading the route.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return app;
    }
}