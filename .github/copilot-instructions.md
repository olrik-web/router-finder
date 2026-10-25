# Copilot Instructions for RouteFinder

- When a change touches the frontend in `RouteFinder/wwwroot`, prefer verifying the user-visible behavior with `playwright-cli` against the local app.
- Start the app with `dotnet run --project RouteFinder/RouteFinder.csproj --launch-profile http` and use `http://localhost:5156` unless the repo configuration changes.
- Use `playwright-cli snapshot` before interacting so element refs are current.
- Prefer testing real user flows such as place search, map selection, route generation, status messages and GPX/GeoJSON downloads over DOM-only assertions.
- Pure browser logic (file export, formatting) lives in ES modules under `RouteFinder/wwwroot/js` and is unit tested with `npm test` (`node --test`).
- If a frontend flow depends on an external service and a real end-to-end check is too brittle, say that explicitly and fall back to the narrowest practical mocked or partial verification.
- In PowerShell, prefer single-quoted `playwright-cli` JavaScript snippets and double quotes inside the JavaScript.
