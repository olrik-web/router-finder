# 🗺️ Route Finder

A simple web app for generating circular running, walking, and cycling routes.

**Live Demo:
** [https://route-finder-dhbwfqbkguctg8cp.northeurope-01.azurewebsites.net/](https://route-finder-dhbwfqbkguctg8cp.northeurope-01.azurewebsites.net/)
*(if I haven't shut it down yet)*

## Features

- 🎯 **Click-to-select location** or use your current location
- 🔄 **Generate circular routes** that start and end at the same point
- 🧭 **Retry-backed route generation** to avoid obvious OpenRouteService round-trip outliers
- 🚶 **Multiple activity types**: Walking, running, hiking, cycling (regular/road/mountain/electric), driving
- 📏 **Customizable distance**: 1-50km routes
- 💾 **Download routes** as GPX (for Garmin/Strava), GeoJSON, or JSON
- ♿ **Inline status messaging** with keyboard-accessible download controls
- 🛡️ **Validation and throttling** with clear API problem responses
- 🎲 **Random route generation** - get a different route each time with the same settings

## Why I Built This

I just wanted a simple tool to find new running routes near my house. This is a fun side project to scratch that itch while learning more about .NET and spatial data.

## Tech Stack

**Backend:**

- ASP.NET Core 10.0 minimal APIs
- [OpenRouteService API](https://openrouteservice.org) for route generation
- xUnit + WebApplicationFactory for automated API tests

**Frontend:**

- Vanilla JavaScript
- [Leaflet.js](https://leafletjs.com/) for interactive maps
- [OpenStreetMap](https://www.openstreetmap.org) tiles

**Hosting:**

- Azure App Service (Free F1 tier)
- GitHub Actions for CI/CD

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- OpenRouteService API key (free at [openrouteservice.org](https://openrouteservice.org/dev/#/signup))

### Local Development

1. **Clone the repo**
   ```bash
   git clone https://github.com/yourusername/route-finder.git
   cd route-finder
   ```

2. **Set up your API key**

   Using user secrets:
   ```bash
   dotnet user-secrets set "OpenRouteService:ApiKey" "your-api-key-here"
   ```

   Or add to `appsettings.Development.json`:
   ```json
   {
     "OpenRouteService": {
       "ApiKey": "your-api-key-here"
     }
   }
   ```

3. **Run the app**
   ```bash
   dotnet run --project RouteFinder/RouteFinder.csproj
   ```

4. **Open in browser**
   ```
   http://localhost:5156
   ```

5. **Run the test suite**
    ```bash
    dotnet test RouteFinder.sln --configuration Release
    ```

## API Endpoints

### `POST /api/routes/generate`

Generate a circular route.

**Request:**

```json
{
  "latitude": 56.1572,
  "longitude": 10.2107,
  "distance": 5.0,
  "profile": "foot-walking"
}
```

**Response:**

```json
{
  "coordinates": [
    {
      "latitude": 56.1572,
      "longitude": 10.2107
    },
    ...
  ],
  "distance": 5.2,
  "duration": 3600,
  "summary": "5.2km route",
  "seed": 123456789
}
```

### `POST /api/routes/download?format={gpx|geojson|json}`

Download the same route in different formats.

**Request:** Same as generate endpoint, but includes `seed` to get the exact same route.

## API Behavior

- Invalid latitude, longitude, distance, profile, or download format values return `400` validation responses.
- Upstream provider failures return problem responses instead of raw exception text.
- The route endpoints are rate limited per IP by default:
    - `POST /api/routes/generate`: `6` requests per `60` seconds
    - `POST /api/routes/download`: `12` requests per `60` seconds
- Rate-limited requests return `429 Too Many Requests` with `Retry-After` metadata.

## Deployment

GitHub Actions now uses a gated flow:

- Pull requests to `main` run restore, build, and test.
- Pushes to `main` run restore, build, test, publish, and deploy.

**Required Azure configuration:**

- App Service: Free F1 tier
- Environment variable: `OpenRouteService__ApiKey`

**Optional rate limiting configuration:**

- `RateLimiting__Generate__PermitLimit`
- `RateLimiting__Generate__WindowSeconds`
- `RateLimiting__Download__PermitLimit`
- `RateLimiting__Download__WindowSeconds`

If you host the app behind another proxy, preserve forwarded headers so IP-based rate limiting continues to use the real client address.
