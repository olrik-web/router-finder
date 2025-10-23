# 🗺️ Route Finder

A simple web app for generating circular running, walking, and cycling routes. Built because I wanted to discover new routes in my neighborhood without paying for premium route planning services.

**Live Demo:** [https://route-finder-dhbwfqbkguctg8cp.northeurope-01.azurewebsites.net/](https://route-finder-dhbwfqbkguctg8cp.northeurope-01.azurewebsites.net/) *(if I haven't shut it down yet)*

## Features

- 🎯 **Click-to-select location** or use your current location
- 🔄 **Generate circular routes** that start and end at the same point
- 🚶 **Multiple activity types**: Walking, running, hiking, cycling (regular/road/mountain/electric), driving
- 📏 **Customizable distance**: 1-50km routes
- 💾 **Download routes** as GPX (for Garmin/Strava), GeoJSON, or JSON
- 🎲 **Random route generation** - get a different route each time with the same settings

## Why I Built This

Most route planning apps either:
- Require a paid subscription for circular routes
- Only let you plan point-to-point routes
- Have clunky UIs

I just wanted a simple tool to find new running routes near my house. This is a fun side project to scratch that itch while learning more about .NET and spatial data.

## Tech Stack

**Backend:**
- ASP.NET Core 9.0
- [OpenRouteService API](https://openrouteservice.org) for route generation

**Frontend:**
- Vanilla JavaScript (no frameworks)
- [Leaflet.js](https://leafletjs.com/) for interactive maps
- [OpenStreetMap](https://www.openstreetmap.org) tiles

**Hosting:**
- Azure App Service (Free F1 tier)
- GitHub Actions for CI/CD

## Getting Started

### Prerequisites

- .NET 9.0 SDK
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
   dotnet run
   ```

4. **Open in browser**
   ```
   https://localhost:5156
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
    { "latitude": 56.1572, "longitude": 10.2107 },
    ...
  ],
  "distance": 5.2,
  "duration": 3600,
  "description": "5.2km route",
  "seed": 123456789
}
```

### `POST /api/routes/download?format={gpx|geojson|json}`
Download the same route in different formats.

**Request:** Same as generate endpoint, but includes `seed` to get the exact same route.

## Deployment

The app auto-deploys to Azure App Service when pushing to `main` via GitHub Actions.

**Required Azure configuration:**
- App Service: Free F1 tier
- Environment variable: `OpenRouteService__ApiKey`
