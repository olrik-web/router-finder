using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route.Services;

public sealed partial class OpenRouteService(HttpClient httpClient, ILogger<OpenRouteService> logger) : IOpenRouteService
{
    private const int MaxRoundTripAttempts = 5;

    // https://giscience.github.io/openrouteservice/api-reference/error-codes
    private const int RouteNotFoundErrorCode = 2009;
    private const int PointNotFoundErrorCode = 2010;

    /// <summary>How long attempts after the first usable route may take in total.</summary>
    internal TimeSpan RetryBudget { get; init; } = TimeSpan.FromSeconds(10);

    public async Task<RouteResponse> GenerateRoundTripAsync(
        double latitude,
        double longitude,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default)
    {
        var targetDistanceMeters = distanceKm * 1000;
        GeneratedRoute? bestRoute = null;
        using var retryBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // OpenRouteService round trips are sometimes far longer or shorter than asked for.
        // Try a few random seeds and keep the closest one.
        for (var attempt = 1; attempt <= MaxRoundTripAttempts; attempt++)
        {
            GeneratedRoute candidate;
            try
            {
                candidate = await RequestRoundTripAsync(
                    latitude,
                    longitude,
                    targetDistanceMeters,
                    profile,
                    Random.Shared.Next(),
                    bestRoute is null ? cancellationToken : retryBudget.Token);
            }
            catch (RouteProviderException ex) when (bestRoute is not null)
            {
                // A later attempt failed, but an earlier one gave us a route. Use that instead of failing.
                LogAttemptFailed(logger, attempt, profile, ex.Error);
                break;
            }
            catch (OperationCanceledException) when (bestRoute is not null && !cancellationToken.IsCancellationRequested)
            {
                LogRetryBudgetUsedUp(logger, attempt, profile);
                break;
            }

            if (bestRoute is null)
            {
                // Retries only try to improve on this route, so together they get a fixed time budget
                // instead of the full HTTP timeout each.
                retryBudget.CancelAfter(RetryBudget);
            }

            if (bestRoute is null || GetDistanceDelta(candidate, targetDistanceMeters) < GetDistanceDelta(bestRoute, targetDistanceMeters))
            {
                bestRoute = candidate;
            }

            if (IsRouteDistanceAcceptable(candidate, targetDistanceMeters))
            {
                return CreateRouteResponse(candidate);
            }

            LogOutlierDiscarded(logger, attempt, profile, distanceKm, candidate.DistanceMeters / 1000);
        }

        LogUsingBestCandidate(logger, profile, distanceKm, bestRoute!.DistanceMeters / 1000);
        return CreateRouteResponse(bestRoute);
    }

    private async Task<GeneratedRoute> RequestRoundTripAsync(
        double latitude,
        double longitude,
        double lengthMeters,
        string profile,
        int seed,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            coordinates = new[] { new[] { longitude, latitude } },
            options = new
            {
                round_trip = new
                {
                    length = lengthMeters,
                    points = 2,
                    seed
                }
            }
        };

        // Serialize up front so the request has a Content-Length instead of a chunked body.
        using var content = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonSerializerOptions.Web),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync($"/v2/directions/{profile}/geojson", content, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new RouteProviderException(RouteProviderError.Unavailable, "Could not reach OpenRouteService.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RouteProviderException(RouteProviderError.Timeout, "OpenRouteService timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorCode = await TryReadErrorCodeAsync(response, cancellationToken);
                var error = errorCode is PointNotFoundErrorCode or RouteNotFoundErrorCode
                    ? RouteProviderError.NoRoutablePoint
                    : RouteProviderError.Unavailable;

                throw new RouteProviderException(
                    error,
                    $"OpenRouteService returned {(int)response.StatusCode} with error code {errorCode?.ToString(CultureInfo.InvariantCulture) ?? "none"}.");
            }

            RouteResponseRoot? route;
            try
            {
                route = await response.Content.ReadFromJsonAsync<RouteResponseRoot>(cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new RouteProviderException(RouteProviderError.InvalidResponse, "OpenRouteService returned invalid JSON.", ex);
            }

            return ParseRoute(route);
        }
    }

    private static GeneratedRoute ParseRoute(RouteResponseRoot? route)
    {
        var feature = route?.Features?.FirstOrDefault();

        var coordinates = feature?.Geometry?.Coordinates?
            .OfType<List<double>>()
            .Where(c => c.Count >= 2)
            .Select(c => new Coordinate(c[1], c[0]))
            .ToList() ?? [];

        if (coordinates.Count == 0)
        {
            throw new RouteProviderException(RouteProviderError.InvalidResponse, "The route did not include any coordinates.");
        }

        var summary = feature?.Properties?.Summary;
        var segments = feature?.Properties?.Segments?.OfType<RouteSegment>().ToList() ?? [];

        var distance = summary?.Distance > 0 ? summary.Distance : segments.Sum(s => s.Distance);
        var duration = summary?.Duration > 0 ? summary.Duration : segments.Sum(s => s.Duration);

        if (distance <= 0 || duration <= 0)
        {
            throw new RouteProviderException(RouteProviderError.InvalidResponse, "The route did not include a distance or duration.");
        }

        return new GeneratedRoute(coordinates, distance, duration);
    }

    private static async Task<int?> TryReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Routing errors look like {"error": {"code": 2010, "message": "..."}}.
        // Auth and quota errors use {"error": "message"} instead, so read the body loosely.
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return document.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("code", out var code)
                   && code.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RouteResponse CreateRouteResponse(GeneratedRoute route) =>
        new(route.Coordinates, route.DistanceMeters / 1000, route.DurationSeconds);

    private static bool IsRouteDistanceAcceptable(GeneratedRoute route, double targetDistanceMeters) =>
        GetDistanceDelta(route, targetDistanceMeters) <= Math.Max(750, targetDistanceMeters * 0.35);

    private static double GetDistanceDelta(GeneratedRoute route, double targetDistanceMeters) =>
        Math.Abs(route.DistanceMeters - targetDistanceMeters);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Discarding round trip attempt {Attempt} for {Profile}: asked for {RequestedKm:F1} km, got {ActualKm:F1} km.")]
    private static partial void LogOutlierDiscarded(ILogger logger, int attempt, string profile, double requestedKm, double actualKm);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Round trip attempt {Attempt} for {Profile} failed with {Error}. Using the best earlier attempt.")]
    private static partial void LogAttemptFailed(ILogger logger, int attempt, string profile, RouteProviderError error);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Round trip attempt {Attempt} for {Profile} ran out of retry time. Using the best earlier attempt.")]
    private static partial void LogRetryBudgetUsedUp(ILogger logger, int attempt, string profile);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No round trip for {Profile} was close to {RequestedKm:F1} km. Returning the closest one, {ActualKm:F1} km.")]
    private static partial void LogUsingBestCandidate(ILogger logger, string profile, double requestedKm, double actualKm);

    private sealed record GeneratedRoute(
        List<Coordinate> Coordinates,
        double DistanceMeters,
        double DurationSeconds);
}
