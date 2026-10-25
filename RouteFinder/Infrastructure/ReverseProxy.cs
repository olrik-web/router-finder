using Microsoft.AspNetCore.HttpOverrides;

namespace RouteFinder.Infrastructure;

public static class ReverseProxy
{
    public const string EnabledKey = "ReverseProxy:Enabled";

    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Azure App Service does not publish fixed front-end addresses, so every proxy is trusted.
            // ForwardLimit = 1 means only the right-most X-Forwarded-For entry is used, which is the
            // one Azure appends. Anything a client sends itself sits further left and is ignored.
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Reads X-Forwarded-* headers only when <c>ReverseProxy:Enabled</c> is true. Without a proxy
    /// in front, a client could set X-Forwarded-For to anything and dodge the rate limit.
    /// </summary>
    public static WebApplication UseReverseProxySupport(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(EnabledKey))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
