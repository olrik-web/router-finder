using RouteFinder.Features.Route;
using RouteFinder.Features.Route.Endpoints;
using RouteFinder.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddReverseProxySupport();
builder.Services.AddRouteFeature(builder.Configuration);

var app = builder.Build();

app.UseReverseProxySupport();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Malformed JSON is the client's fault. Report 400, not 500.
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseRouting();
app.UseRateLimiter();

app.MapStaticAssets();
app.MapHealthChecks("/api/health");
app.MapRouteEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

public partial class Program;
