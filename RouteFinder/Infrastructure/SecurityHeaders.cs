namespace RouteFinder.Infrastructure;

public static class SecurityHeaders
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com; " +
        "style-src 'self' https://unpkg.com; " +
        "img-src 'self' data: https://unpkg.com https://tile.openstreetmap.org; " +
        "connect-src 'self' https://nominatim.openstreetmap.org; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";

            // OpenStreetMap tiles and Nominatim require a Referer that identifies the site.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";

            await next(context);
        });

        return app;
    }
}
