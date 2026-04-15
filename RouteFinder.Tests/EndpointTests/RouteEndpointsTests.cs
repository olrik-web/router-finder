using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RouteFinder.Features.Route.Models;
using RouteFinder.Tests.Infrastructure;

namespace RouteFinder.Tests.EndpointTests;

public class RouteEndpointsTests
{
    [Fact]
    public async Task GenerateRoute_WithInvalidLatitude_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(91, 10.2107, 5, "cycling-regular"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "latitude", "Latitude must be between -90 and 90.");
    }

    [Fact]
    public async Task GenerateRoute_WithInvalidLongitude_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 181, 5, "cycling-regular"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "longitude", "Longitude must be between -180 and 180.");
    }

    [Fact]
    public async Task GenerateRoute_WithInvalidDistance_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 0.5, "cycling-regular"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "distance", "Distance must be between 1 and 50 kilometers.");
    }

    [Fact]
    public async Task GenerateRoute_WithInvalidProfile_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "hoverboard"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "profile", "Profile must be one of: foot-walking, foot-hiking, cycling-regular, cycling-road, cycling-mountain, cycling-electric, driving-car.");
    }

    [Fact]
    public async Task DownloadRoute_WithoutFormat_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/download", new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "format", "Format is required.");
    }

    [Fact]
    public async Task DownloadRoute_WithUnsupportedFormat_ReturnsValidationProblem()
    {
        var routeService = new TestOpenRouteService();
        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/download?format=xml", new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"));

        await AssertValidationProblemAsync(response, HttpStatusCode.BadRequest, "format", "Format must be one of: json, geojson, gpx.");
    }

    [Fact]
    public async Task GenerateRoute_WhenUpstreamReturnsHttpError_ReturnsBadGateway()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => throw new HttpRequestException("upstream failed", null, HttpStatusCode.BadGateway)
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "Unable to generate a route from the routing provider right now.");
    }

    [Fact]
    public async Task GenerateRoute_WhenRequestTimesOut_ReturnsGatewayTimeout()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => throw new TaskCanceledException("timed out")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "Route generation timed out. Please try again.");
    }

    [Fact]
    public async Task GenerateRoute_WhenUpstreamReturnsInvalidJson_ReturnsBadGateway()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => throw new JsonException("bad json")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "The routing provider returned an unreadable response.");
    }

    [Fact]
    public async Task GenerateRoute_WhenUpstreamRoutePayloadIsInvalid_ReturnsBadGateway()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => throw new InvalidOperationException("invalid route")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "The routing provider returned an unexpected route response.");
    }

    [Fact]
    public async Task GenerateRoute_WhenUnexpectedExceptionOccurs_ReturnsInternalServerError()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => throw new Exception("boom")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "An unexpected error occurred while generating the route.");
    }

    [Fact]
    public async Task GenerateRoute_WithValidRequest_ReturnsRoute()
    {
        var routeService = new TestOpenRouteService
        {
            GenerateRouteAsync = (_, _, _, _, _) => Task.FromResult(TestOpenRouteService.CreateRouteResponse(4.8, 1620, 8765))
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/generate", new RouteRequest(56.1572, 10.2107, 5, "cycling-regular"));

        response.EnsureSuccessStatusCode();

        var route = await response.Content.ReadFromJsonAsync<RouteResponse>();

        Assert.NotNull(route);
        Assert.Equal(4.8, route.Distance);
        Assert.Equal(1620, route.Duration);
        Assert.Equal(8765, route.Seed);
        Assert.Equal(2, route.Coordinates.Count);
    }

    [Fact]
    public async Task DownloadRoute_WhenUpstreamReturnsHttpError_ReturnsBadGateway()
    {
        var routeService = new TestOpenRouteService
        {
            DownloadRouteAsync = (_, _, _, _, _, _, _) => throw new HttpRequestException("upstream failed", null, HttpStatusCode.BadGateway)
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/download?format=gpx", new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "Unable to download a route from the routing provider right now.");
    }

    [Fact]
    public async Task DownloadRoute_WhenRequestTimesOut_ReturnsGatewayTimeout()
    {
        var routeService = new TestOpenRouteService
        {
            DownloadRouteAsync = (_, _, _, _, _, _, _) => throw new TaskCanceledException("timed out")
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/download?format=gpx", new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"));

        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "Route download timed out. Please try again.");
    }

    [Fact]
    public async Task DownloadRoute_WithValidRequest_ReturnsFilePayload()
    {
        var expectedContent = Encoding.UTF8.GetBytes("<gpx></gpx>");

        var routeService = new TestOpenRouteService
        {
            DownloadRouteAsync = (_, _, _, _, _, _, _) => Task.FromResult<(byte[] Content, string ContentType)>((expectedContent, "application/gpx+xml"))
        };

        using var factory = new RouteFinderWebApplicationFactory(routeService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/routes/download?format=gpx", new DownloadRouteRequest(56.1572, 10.2107, 5, 1234, "cycling-regular"));

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal("application/gpx+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedContent, content);
        Assert.Contains("route.gpx", response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string key,
        string expectedError)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.True(problem.Errors.TryGetValue(key, out var errors));
        Assert.Contains(expectedError, errors);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string expectedDetail)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatusCode, problem.Status);
        Assert.Equal(expectedDetail, problem.Detail);
    }
}