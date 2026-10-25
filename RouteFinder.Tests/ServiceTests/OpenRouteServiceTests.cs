using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RouteFinder.Features.Route;
using RouteFinder.Features.Route.Services;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.ServiceTests;

public class OpenRouteServiceTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerateRoundTrip_ReturnsFirstAcceptableCandidateWithoutRetrying()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(5100, 1800))));
        var service = CreateService(handler);

        var route = await service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(5.1, route.Distance);
        Assert.Equal(2, route.Coordinates.Count);
    }

    [Fact]
    public async Task GenerateRoundTrip_RetriesUntilAnAcceptableCandidateIsReturned()
    {
        var handler = CreateQueuedHandler(
            CreateJsonResponse(CreateGeoJsonPayload(9000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(4800, 1800)));
        var service = CreateService(handler);

        var route = await service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(4.8, route.Distance);
    }

    [Fact]
    public async Task GenerateRoundTrip_ReturnsClosestCandidateAfterFiveOutliers()
    {
        var handler = CreateQueuedHandler(
            CreateJsonResponse(CreateGeoJsonPayload(95000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(12000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(9000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(8200, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(6800, 2400)));
        var service = CreateService(handler);

        var route = await service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken);

        Assert.Equal(5, handler.CallCount);
        Assert.Equal(6.8, route.Distance);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenLaterAttemptFails_ReturnsBestEarlierCandidate()
    {
        var handler = CreateQueuedHandler(
            CreateJsonResponse(CreateGeoJsonPayload(9000, 2400)),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = CreateService(handler);

        var route = await service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(9, route.Distance);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenFirstAttemptFails_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken));

        Assert.Equal(RouteProviderError.Unavailable, exception.Error);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenPointIsNotRoutable_ThrowsNoRoutablePoint()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(
            """{"error":{"code":2010,"message":"Could not find routable point within a radius of 350.0 meters."}}""",
            HttpStatusCode.NotFound)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.0, 11.0, 5, "foot-walking", CancellationToken));

        Assert.Equal(RouteProviderError.NoRoutablePoint, exception.Error);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenAuthFails_ThrowsUnavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(
            """{"error":"Access to this API has been disallowed"}""",
            HttpStatusCode.Forbidden)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "foot-walking", CancellationToken));

        Assert.Equal(RouteProviderError.Unavailable, exception.Error);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenResponseIsNotJson_ThrowsInvalidResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse("<html>oops</html>")));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "foot-walking", CancellationToken));

        Assert.Equal(RouteProviderError.InvalidResponse, exception.Error);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenRequestTimesOut_ThrowsTimeout()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out"));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "foot-walking", CancellationToken));

        Assert.Equal(RouteProviderError.Timeout, exception.Error);
    }

    [Fact]
    public async Task GenerateRoundTrip_WhenCoordinatesAreMissing_ThrowsInvalidResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse("""
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "geometry": { "type": "LineString", "coordinates": [] },
                  "properties": { "summary": { "distance": 5000, "duration": 1800 }, "segments": [] }
                }
              ]
            }
            """)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<RouteProviderException>(
            () => service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken));

        Assert.Equal(RouteProviderError.InvalidResponse, exception.Error);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateRoundTrip_SendsRoundTripRequestToGeoJsonEndpoint()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(5000, 1800))));
        var service = CreateService(handler);

        await service.GenerateRoundTripAsync(56.1572, 10.2107, 5, "cycling-regular", CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.test/v2/directions/cycling-regular/geojson", request.RequestUri?.ToString());
        Assert.Contains("\"round_trip\":{\"length\":5000", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"coordinates\":[[10.2107,56.1572]]", request.Body, StringComparison.Ordinal);
    }

    private static OpenRouteService CreateService(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };

        return new OpenRouteService(client, NullLogger<OpenRouteService>.Instance);
    }

    private static StubHttpMessageHandler CreateQueuedHandler(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        return new StubHttpMessageHandler((_, _) => Task.FromResult(queue.Dequeue()));
    }

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateGeoJsonPayload(double distanceMeters, double durationSeconds)
    {
        return JsonSerializer.Serialize(new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "LineString",
                        coordinates = new[]
                        {
                            new[] { 10.2107, 56.1572 },
                            new[] { 10.2207, 56.1672 }
                        }
                    },
                    properties = new
                    {
                        summary = new { distance = distanceMeters, duration = durationSeconds },
                        segments = Array.Empty<object>()
                    }
                }
            }
        });
    }
}
