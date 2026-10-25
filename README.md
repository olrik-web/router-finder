# Route Finder

Route Finder makes a loop route from a point you pick on the map. Choose a distance and an activity, and it asks
[openrouteservice](https://openrouteservice.org) for a round trip that starts and ends at the same spot. You can
download the route as GPX for a watch, Strava or Komoot, or as GeoJSON.

I built it to find new running routes near my house, and to learn more about .NET and map data along the way.

Live demo: https://route-finder-dhbwfqbkguctg8cp.northeurope-01.azurewebsites.net/ (it may be shut down)

## What it does

- Set the start point by searching for a place, clicking the map, or using your current location
- Walking, hiking, cycling (regular, road, mountain, e-bike) and driving
- Routes from 1 to 50 km
- A new route each time you press the button, even with the same settings
- Download as GPX or GeoJSON

openrouteservice sometimes returns a loop that is much longer or shorter than asked for. The server asks for up to
five routes and keeps the first one within 35% of the requested distance, or the closest one if none are.

## Tech stack

- ASP.NET Core 10 minimal API, one endpoint
- Plain JavaScript modules and [Leaflet](https://leafletjs.com/) with [OpenStreetMap](https://www.openstreetmap.org)
  tiles, no build step
- [Nominatim](https://nominatim.org) for place search
- xUnit v3 for the API, `node --test` for the browser code
- Azure App Service (Free F1) and GitHub Actions

## Running it locally

You need the .NET 10 SDK and a free openrouteservice API key from
[openrouteservice.org](https://openrouteservice.org/dev/#/signup). Node 22 or newer is only needed for the frontend tests.

```bash
git clone https://github.com/olrik-web/router-finder.git
cd router-finder
dotnet user-secrets set "OpenRouteService:ApiKey" "your-api-key" --project RouteFinder
dotnet run --project RouteFinder
```

Then open http://localhost:5156. In development the OpenAPI document is at `/openapi/v1.json`.

Run the tests:

```bash
dotnet test
npm test
```

## API

`POST /api/routes/generate`

```json
{
  "latitude": 56.1572,
  "longitude": 10.2107,
  "distance": 5,
  "profile": "foot-walking"
}
```

`profile` is optional and defaults to `foot-walking`. The response holds the route as a list of points, the length in
km and the estimated time in seconds:

```json
{
  "coordinates": [{ "latitude": 56.1572, "longitude": 10.2107 }],
  "distance": 5.2,
  "duration": 3600
}
```

Errors come back as [problem details](https://www.rfc-editor.org/rfc/rfc9457):

| Status | When |
| ------ | ---- |
| 400 | A value is missing or out of range |
| 422 | There is no road or path near the start point |
| 429 | Too many requests from one IP (6 per minute by default) |
| 502 | openrouteservice failed or sent something unreadable |
| 504 | openrouteservice did not answer in time |

`GET /api/health` returns `Healthy`.

## Configuration

| Setting | Default | Notes |
| ------- | ------- | ----- |
| `OpenRouteService:ApiKey` | | Required |
| `OpenRouteService:TimeoutSeconds` | 15 | Per request to openrouteservice |
| `RateLimiting:Generate:PermitLimit` | 6 | Requests per window, per IP |
| `RateLimiting:Generate:WindowSeconds` | 60 | |
| `ReverseProxy:Enabled` | `false`, `true` in Production | Read the client IP from `X-Forwarded-For` |

On Azure, set these as app settings with `__` in place of `:`, for example `OpenRouteService__ApiKey`.

Only turn on `ReverseProxy:Enabled` when the app runs behind a proxy that adds `X-Forwarded-For`, like Azure App
Service does. Without a proxy, a client could send any IP in that header and get around the rate limit.

## Deployment

The workflow in `.github/workflows` builds and tests every pull request to `main`. Pushes to `main` also publish the
app and deploy it to Azure App Service.
