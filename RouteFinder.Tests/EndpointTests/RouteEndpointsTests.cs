using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RouteFinder.Features.Route;
using RouteFinder.Features.Route.Models;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.EndpointTests;

public class RouteEndpointsTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(91, 10.2107, 5, "cycling-regular", "Latitude", "Latitude must be between -90 and 90.")]
    [InlineData(56.1572, 181, 5, "cycling-regular", "Longitude", "Longitude must be between -180 and 180.")]
    [InlineData(56.1572, 10.2107, 0.5, "cycling-regular", "Distance", "Distance must be between 1 and 50 km.")]
    [InlineData(56.1572, 10.2107, 51, "cycling-regular", "Distance", "Distance must be between 1 and 50 km.")]
    [InlineData(56.1572, 10.2107, 5, "hoverboard", "Profile", RouteProfiles.ValidationMessage)]
    public async Task GenerateRoute_WithInvalidValue_ReturnsValidationProblem(
        double latitude, double longitude, double distance, string profile, string key, string expectedError)
    {
        using var factory = new RouteFinderWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes/generate",
            new RouteRequest(latitude, longitude, distance, profile),
            CancellationToken);

        await AssertValidationProblemAsync(response, key, expectedError);
    }

    [Fact]
    public async Task GenerateRoute_WithMissingLatitude_ReturnsValidationProblem()
    {
        using var factory = new RouteFinderWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes/generate",
            new { longitude = 10.2107, distance = 5 },
            CancellationToken);

        await AssertValidationProblemAsync(response, "Latitude", "Latitude is required.");
    }

    [Fact]
    public async Task GenerateRoute_WithMalformedJson_ReturnsBadRequestProblem()
    {
        using var factory = new RouteFinderWebApplicationFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("""{"latitude": 56""", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/routes/generate", content, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GenerateRoute_WithoutProfile_UsesWalking()
    {
        string? usedProfile = null;
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, distanceKm, profile, _) =>
            {
                usedProfile = profile;
                return Task.FromResult(TestOpenRouteService.CreateRouteResponse(distanceKm));
            }
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes/generate",
            new { latitude = 56.1572, longitude = 10.2107, distance = 5 },
            CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(RouteProfiles.FootWalking, usedProfile);
    }

    [Theory]
    [InlineData("Cycling-Regular")]
    [InlineData(" cycling-regular ")]
    [InlineData("CYCLING-REGULAR")]
    public async Task GenerateRoute_WithDifferentlyCasedProfile_SendsCanonicalProfile(string profile)
    {
        string? usedProfile = null;
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, distanceKm, usedValue, _) =>
            {
                usedProfile = usedValue;
                return Task.FromResult(TestOpenRouteService.CreateRouteResponse(distanceKm));
            }
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes/generate",
            new RouteRequest(56.1572, 10.2107, 5, profile),
            CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(RouteProfiles.CyclingRegular, usedProfile);
    }

    [Fact]
    public async Task GenerateRoute_WhenProviderFails_KeepsSecurityHeaders()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, _, _, _) => throw new RouteProviderException(RouteProviderError.Unavailable, "provider failed")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", ValidRequest, CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData(RouteProviderError.NoRoutablePoint, HttpStatusCode.UnprocessableEntity, "There is no road or path near this point. Pick a spot closer to a street or trail.")]
    [InlineData(RouteProviderError.Timeout, HttpStatusCode.GatewayTimeout, "The routing service took too long to answer. Try again.")]
    [InlineData(RouteProviderError.InvalidResponse, HttpStatusCode.BadGateway, "The routing service sent back a route that could not be read. Try again.")]
    [InlineData(RouteProviderError.Unavailable, HttpStatusCode.BadGateway, "The routing service is not available right now. Try again in a moment.")]
    public async Task GenerateRoute_WhenProviderFails_ReturnsMatchingProblem(
        RouteProviderError error, HttpStatusCode expectedStatusCode, string expectedDetail)
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, _, _, _) => throw new RouteProviderException(error, "provider failed")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", ValidRequest, CancellationToken);

        await AssertProblemAsync(response, expectedStatusCode, expectedDetail);
    }

    [Fact]
    public async Task GenerateRoute_WhenUnexpectedExceptionOccurs_ReturnsProblemWithoutExceptionText()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, _, _, _) => throw new InvalidOperationException("secret internals")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", ValidRequest, CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret internals", await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRoute_WithValidRequest_ReturnsRoute()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRoute = (_, _, _, _, _) => Task.FromResult(TestOpenRouteService.CreateRouteResponse(4.8, 1620))
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", ValidRequest, CancellationToken);

        response.EnsureSuccessStatusCode();

        var route = await response.Content.ReadFromJsonAsync<RouteResponse>(CancellationToken);

        Assert.NotNull(route);
        Assert.Equal(4.8, route.Distance);
        Assert.Equal(1620, route.Duration);
        Assert.Equal(2, route.Coordinates.Count);
    }

    [Fact]
    public async Task Root_ServesIndexPageWithSecurityHeaders()
    {
        using var factory = new RouteFinderWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        using var factory = new RouteFinderWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health", CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    private static readonly RouteRequest ValidRequest = new(56.1572, 10.2107, 5, RouteProfiles.CyclingRegular);

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string key, string expectedError)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken);

        Assert.NotNull(problem);
        Assert.True(problem.Errors.TryGetValue(key, out var errors), $"No error for '{key}'. Keys: {string.Join(", ", problem.Errors.Keys)}");
        Assert.Contains(expectedError, errors);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode, string expectedDetail)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatusCode, problem.Status);
        Assert.Equal(expectedDetail, problem.Detail);
    }
}
