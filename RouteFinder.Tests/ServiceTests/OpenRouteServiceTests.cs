using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RouteFinder.Features.Route.Services;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.ServiceTests;

public class OpenRouteServiceTests
{
    [Fact]
    public async Task GenerateCircularRoute_ReturnsFirstAcceptableCandidateWithoutRetrying()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(5100, 1800))));
        var service = CreateService(handler);

        var route = await service.GenerateCircularRoute(56.1572, 10.2107, 5, "cycling-regular");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(5.1, route.Distance);
        Assert.Equal(2, route.Coordinates.Count);
    }

    [Fact]
    public async Task GenerateCircularRoute_RetriesUntilAnAcceptableCandidateIsReturned()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateJsonResponse(CreateGeoJsonPayload(9000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(4800, 1800))
        ]);

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(responses.Dequeue()));
        var service = CreateService(handler);

        var route = await service.GenerateCircularRoute(56.1572, 10.2107, 5, "cycling-regular");

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(4.8, route.Distance);
    }

    [Fact]
    public async Task GenerateCircularRoute_ReturnsClosestCandidateAfterFiveOutliers()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateJsonResponse(CreateGeoJsonPayload(95000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(12000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(9000, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(8200, 2400)),
            CreateJsonResponse(CreateGeoJsonPayload(6800, 2400))
        ]);

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(responses.Dequeue()));
        var service = CreateService(handler);

        var route = await service.GenerateCircularRoute(56.1572, 10.2107, 5, "cycling-regular");

        Assert.Equal(5, handler.CallCount);
        Assert.Equal(6.8, route.Distance);
    }

    [Fact]
    public async Task GenerateCircularRoute_AcceptsCandidateWithinThirtyFivePercentThreshold()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(6700, 1800))));
        var service = CreateService(handler);

        var route = await service.GenerateCircularRoute(56.1572, 10.2107, 5, "cycling-regular");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(6.7, route.Distance);
    }

    [Fact]
    public async Task GenerateCircularRoute_AcceptsCandidateWithinSevenHundredFiftyMeterFloor()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(1700, 900))));
        var service = CreateService(handler);

        var route = await service.GenerateCircularRoute(56.1572, 10.2107, 1, "cycling-regular");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1.7, route.Distance);
    }

    [Fact]
    public async Task GenerateCircularRoute_ThrowsWhenCoordinatesAreMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse("""
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "geometry": {
                    "type": "LineString",
                    "coordinates": []
                  },
                  "properties": {
                    "summary": {
                      "distance": 5000,
                      "duration": 1800
                    },
                    "segments": []
                  }
                }
              ]
            }
            """)));

        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateCircularRoute(56.1572, 10.2107, 5, "cycling-regular"));

        Assert.Equal("Upstream route response did not include any coordinates.", exception.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateCircularRoute_SendsGeoJsonRequestsToTheExpectedEndpoint()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateJsonResponse(CreateGeoJsonPayload(5000, 1800))));
        var service = CreateService(handler);

        await service.GenerateCircularRoute(56.1572, 10.2107, 5, "Cycling-Regular");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.test/v2/directions/cycling-regular/geojson", request.RequestUri?.ToString());
        Assert.Contains("\"length\":5000", request.Body);
    }

    private static OpenRouteService CreateService(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };

        return new OpenRouteService(client, NullLogger<OpenRouteService>.Instance);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
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
                        summary = new
                        {
                            distance = distanceMeters,
                            duration = durationSeconds
                        },
                        segments = Array.Empty<object>()
                    }
                }
            }
        });
    }
}