using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RouteFinder.Features.Route.Models;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.EndpointTests;

public class RateLimitingTests
{
    [Fact]
    public async Task GenerateRoute_ExceedingLimitForSameIp_ReturnsTooManyRequests()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var successfulResponses = new List<HttpResponseMessage>();
        for (var index = 0; index < 6; index++)
        {
            successfulResponses.Add(await SendGenerateRequestAsync(client, "203.0.113.10"));
        }

        var throttledResponse = await SendGenerateRequestAsync(client, "203.0.113.10");

        Assert.All(successfulResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
        Assert.True(throttledResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.True(int.TryParse(retryAfterValues.Single(), out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too many route generation requests from this IP. Please wait before trying again.", problem.Detail);
    }

    [Fact]
    public async Task GenerateRoute_DifferentIpsReceiveIndependentBuckets()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var index = 0; index < 6; index++)
        {
            var allowedResponse = await SendGenerateRequestAsync(client, "203.0.113.10");
            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        var throttledResponse = await SendGenerateRequestAsync(client, "203.0.113.10");
        var differentIpResponse = await SendGenerateRequestAsync(client, "198.51.100.24");

        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, differentIpResponse.StatusCode);
    }

    [Fact]
    public async Task DownloadRoute_UsesItsOwnRateLimitPolicy()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var successfulResponses = new List<HttpResponseMessage>();
        for (var index = 0; index < 12; index++)
        {
            successfulResponses.Add(await SendDownloadRequestAsync(client, "203.0.113.20"));
        }

        var throttledResponse = await SendDownloadRequestAsync(client, "203.0.113.20");

        Assert.All(successfulResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Too many route download requests from this IP. Please wait before trying again.", problem.Detail);
    }

    private static RouteFinderWebApplicationFactory CreateFactory()
    {
        return new RouteFinderWebApplicationFactory(new TestOpenRouteService());
    }

    private static Task<HttpResponseMessage> SendGenerateRequestAsync(HttpClient client, string forwardedFor)
    {
        return SendJsonAsync(
            client,
            "/api/routes/generate",
            new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"),
            forwardedFor);
    }

    private static Task<HttpResponseMessage> SendDownloadRequestAsync(HttpClient client, string forwardedFor)
    {
        return SendJsonAsync(
            client,
            "/api/routes/download?format=gpx",
            new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"),
            forwardedFor);
    }

    private static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string path, object payload, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);

        return client.SendAsync(request);
    }
}