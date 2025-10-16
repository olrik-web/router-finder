using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RouteFinder.Features.Route.Models;

namespace RouteFinder.Features.Route.Services;

public class OpenRouteService(HttpClient httpClient, IOptions<OpenRouteServiceOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RouteResponse> GenerateCircularRoute(
        double lat,
        double lon,
        double distanceKm,
        string profile)
    {
        var seed = Random.Shared.Next();
        
        var requestBody = new
        {
            coordinates = new[] { new[] { lon, lat } },
            options = new
            {
                round_trip = new
                {
                    length = distanceKm * 1000, // Convert to meters
                    points = 2,
                    seed
                },
                // avoid_features = new[] { "highways" }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v2/directions/{profile}/geojson?api_key={options.Value.ApiKey}")
        {
            Content = content
        };

        var response = await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var route = JsonSerializer.Deserialize<RouteResponseRoot>(json, JsonOptions);

        if (route == null || route.Features.Count == 0)
            throw new InvalidOperationException("Invalid route response");

        var firstFeature = route.Features[0];
        var coords = firstFeature.Geometry.Coordinates
            .Select(c => new Coordinate(c[1], c[0]))
            .ToList();

        var firstSegment = firstFeature.Properties.Segments.FirstOrDefault();

        var distance = firstSegment?.Distance ?? 0;
        var duration = firstSegment?.Duration ?? 0;

        return new RouteResponse(coords, distance / 1000, duration, $"{distance / 1000:F1}km route", seed);
    }
    
    public async Task<(byte[] Content, string ContentType)> DownloadRouteFileAsync(
        double lat,
        double lon,
        double distanceKm,
        string profile,
        string format,
        int seed)
    {
        var validFormats = new[] { "json", "geojson", "gpx" };
        if (!validFormats.Contains(format))
            throw new ArgumentException($"Invalid format '{format}'. Valid options: json, geojson, gpx");

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

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var url = $"/v2/directions/{profile}/{format}?api_key={options.Value.ApiKey}";
        var response = await httpClient.PostAsync(url, content);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        var contentType = format switch
        {
            "json" or "geojson" => "application/json",
            "gpx" => "application/gpx+xml",
            _ => "application/octet-stream"
        };

        return (bytes, contentType);
    }
}