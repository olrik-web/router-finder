using RouteFinder.Features.Route;
using RouteFinder.Features.Route.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapRouteEndpoints();

app.Run();

public partial class Program
{
}