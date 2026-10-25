using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RouteFinder.Features.Route.Models;
using RouteFinder.Infrastructure;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.EndpointTests;

public class RateLimitingTests
{
    private const int PermitLimit = 6;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerateRoute_ExceedingLimitForSameIp_ReturnsTooManyRequests()
    {
        using var factory = CreateFactory(reverseProxyEnabled: true);
        using var client = factory.CreateClient();

        for (var index = 0; index < PermitLimit; index++)
        {
            var allowed = await SendGenerateRequestAsync(client, "203.0.113.10");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var throttled = await SendGenerateRequestAsync(client, "203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(throttled.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.True(int.TryParse(retryAfterValues.Single(), out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);

        var problem = await throttled.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal($"Too many routes requested. Try again in {retryAfterSeconds} seconds.", problem.Detail);
    }

    [Fact]
    public async Task GenerateRoute_BehindProxy_DifferentClientIpsGetSeparateLimits()
    {
        using var factory = CreateFactory(reverseProxyEnabled: true);
        using var client = factory.CreateClient();

        for (var index = 0; index < PermitLimit; index++)
        {
            await SendGenerateRequestAsync(client, "203.0.113.10");
        }

        var throttled = await SendGenerateRequestAsync(client, "203.0.113.10");
        var otherClient = await SendGenerateRequestAsync(client, "198.51.100.24");

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherClient.StatusCode);
    }

    [Fact]
    public async Task GenerateRoute_WithoutProxy_IgnoresSpoofedForwardedFor()
    {
        using var factory = CreateFactory(reverseProxyEnabled: false);
        using var client = factory.CreateClient();

        for (var index = 0; index < PermitLimit; index++)
        {
            await SendGenerateRequestAsync(client, $"203.0.113.{index}");
        }

        var spoofed = await SendGenerateRequestAsync(client, "198.51.100.99");

        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    private static RouteFinderWebApplicationFactory CreateFactory(bool reverseProxyEnabled)
    {
        return new RouteFinderWebApplicationFactory(configurationOverrides: new Dictionary<string, string?>
        {
            [ReverseProxy.EnabledKey] = reverseProxyEnabled.ToString(),
            ["RateLimiting:Generate:PermitLimit"] = PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static Task<HttpResponseMessage> SendGenerateRequestAsync(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/routes/generate")
        {
            Content = JsonContent.Create(new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"))
        };

        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);

        return client.SendAsync(request, CancellationToken);
    }
}
