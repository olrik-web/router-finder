using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route.Services;

public class OpenRouteService(HttpClient httpClient, ILogger<OpenRouteService> logger)
{
    private const int MaxRoundTripAttempts = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RouteResponse> GenerateCircularRoute(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        CancellationToken cancellationToken = default)
    {
        var normalizedProfile = RouteValidation.NormalizeProfile(profile);
        var targetDistanceMeters = distanceKm * 1000;
        GeneratedRouteData? bestRoute = null;

        for (var attempt = 1; attempt <= MaxRoundTripAttempts; attempt++)
        {
            var seed = Random.Shared.Next();
            var candidate = await RequestCircularRouteAsync(
                lat,
                lon,
                distanceKm,
                normalizedProfile,
                seed,
                cancellationToken);

            if (bestRoute == null || GetDistanceDelta(candidate.DistanceMeters, targetDistanceMeters) < GetDistanceDelta(bestRoute.DistanceMeters, targetDistanceMeters))
            {
                bestRoute = candidate;
            }

            if (IsRouteDistanceAcceptable(candidate.DistanceMeters, targetDistanceMeters))
            {
                return CreateRouteResponse(candidate);
            }

            logger.LogWarning(
                "Discarding outlier round-trip candidate on attempt {Attempt} for profile {Profile}. Requested {RequestedDistanceKm:F1} km, received {ActualDistanceKm:F1} km.",
                attempt,
                normalizedProfile,
                distanceKm,
                candidate.DistanceMeters / 1000);
        }

        if (bestRoute == null)
        {
            throw new InvalidOperationException("Upstream route response did not produce any usable routes.");
        }

        logger.LogWarning(
            "Returning best available round-trip candidate for profile {Profile}. Requested {RequestedDistanceKm:F1} km, best result {ActualDistanceKm:F1} km after {AttemptCount} attempts.",
            normalizedProfile,
            distanceKm,
            bestRoute.DistanceMeters / 1000,
            MaxRoundTripAttempts);

        return CreateRouteResponse(bestRoute);
    }

    public async Task<(byte[] Content, string ContentType)> DownloadRouteFileAsync(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        string format,
        int seed,
        CancellationToken cancellationToken = default)
    {
        if (!RouteValidation.IsSupportedFormat(format))
        {
            throw new ArgumentException("Unsupported route download format.", nameof(format));
        }

        var normalizedProfile = RouteValidation.NormalizeProfile(profile);
        var normalizedFormat = RouteValidation.NormalizeFormat(format);

        using var content = CreateRoundTripRequestBody(lat, lon, distanceKm, seed);
        using var response = await httpClient.PostAsync(
            $"/v2/directions/{normalizedProfile}/{normalizedFormat}",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var contentType = normalizedFormat switch
        {
            "json" => "application/json",
            "geojson" => "application/geo+json",
            "gpx" => "application/gpx+xml",
            _ => "application/octet-stream"
        };

        return (bytes, contentType);
    }

    private async Task<GeneratedRouteData> RequestCircularRouteAsync(
        double lat,
        double lon,
        double distanceKm,
        string normalizedProfile,
        int seed,
        CancellationToken cancellationToken)
    {
        using var content = CreateRoundTripRequestBody(lat, lon, distanceKm, seed);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v2/directions/{normalizedProfile}/geojson")
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var route = await JsonSerializer.DeserializeAsync<RouteResponseRoot>(responseStream, JsonOptions, cancellationToken);

        var firstFeature = route?.Features?.FirstOrDefault();
        if (firstFeature?.Geometry?.Coordinates == null || firstFeature.Geometry.Coordinates.Count == 0)
        {
            throw new InvalidOperationException("Upstream route response did not include any coordinates.");
        }

        var coords = firstFeature.Geometry.Coordinates
            .Where(c => c.Count >= 2)
            .Select(c => new Coordinate(c[1], c[0]))
            .ToList();

        if (coords.Count == 0)
        {
            throw new InvalidOperationException("Upstream route response did not include usable coordinates.");
        }

        var summary = firstFeature.Properties?.Summary;
        var segments = firstFeature.Properties?.Segments ?? [];

        var distance = summary?.Distance > 0
            ? summary.Distance
            : segments.Sum(segment => segment.Distance);

        var duration = summary?.Duration > 0
            ? summary.Duration
            : segments.Sum(segment => segment.Duration);

        if (distance <= 0 || duration <= 0)
        {
            throw new InvalidOperationException("Upstream route response did not include route summary information.");
        }

        return new GeneratedRouteData(coords, distance, duration, seed);
    }

    private static RouteResponse CreateRouteResponse(GeneratedRouteData route)
    {
        return new RouteResponse(
            route.Coordinates,
            route.DistanceMeters / 1000,
            route.DurationSeconds,
            $"{route.DistanceMeters / 1000:F1}km route",
            route.Seed);
    }

    private static bool IsRouteDistanceAcceptable(double actualDistanceMeters, double targetDistanceMeters)
    {
        return GetDistanceDelta(actualDistanceMeters, targetDistanceMeters) <= Math.Max(750, targetDistanceMeters * 0.35);
    }

    private static double GetDistanceDelta(double actualDistanceMeters, double targetDistanceMeters)
    {
        return Math.Abs(actualDistanceMeters - targetDistanceMeters);
    }

    private static StringContent CreateRoundTripRequestBody(double lat, double lon, double distanceKm, int seed)
    {
        var requestBody = new
        {
            coordinates = new[] { new[] { lon, lat } },
            options = new
            {
                round_trip = new
                {
                    length = distanceKm * 1000,
                    points = 2,
                    seed
                }
            }
        };

        return new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );
    }

    private sealed record GeneratedRouteData(
        List<Coordinate> Coordinates,
        double DistanceMeters,
        double DurationSeconds,
        int Seed);
}