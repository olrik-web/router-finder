using System.Net;

namespace RouteFinder.Tests.Infrastructure;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    public int CallCount => Requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body));

        var response = await handler(request, cancellationToken);
        response.RequestMessage = request;
        return response;
    }
}

public sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, string? Body);