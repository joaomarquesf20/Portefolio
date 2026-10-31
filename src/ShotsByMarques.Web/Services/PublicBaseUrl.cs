namespace ShotsByMarques.Web.Services;

/// <summary>
/// Resolves the public base URL of the current request, honouring the
/// reverse-proxy forwarded headers (X-Forwarded-Host / X-Forwarded-Proto) —
/// e.g. an ngrok domain — so absolute redirect URLs such as Stripe's
/// success_url point to the public domain instead of the local bind address
/// (the classic "post-payment localhost" black screen on mobile).
///
/// UseForwardedHeaders (Program.cs) already rewrites Request.Host/Scheme from
/// these headers; the explicit header lookup is a defensive belt-and-braces
/// fallback that also works if the middleware config ever drifts.
/// </summary>
public static class PublicBaseUrl
{
    public static string Of(HttpRequest request)
    {
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? (request.Host.Value ?? string.Empty);
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
            ?? request.Scheme;

        // A proxy chain can forward several hosts comma-separated — take the
        // first one (the original client-facing public host).
        host = host.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        scheme = scheme.Split(',')[0].Trim();

        return $"{scheme}://{host}";
    }
}
