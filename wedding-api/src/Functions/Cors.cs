using Microsoft.Azure.Functions.Worker.Http;

namespace WeddingApi.Functions;

public static class Cors
{
    private static readonly HashSet<string> AllowedOrigins = new(StringComparer.Ordinal)
    {
        "https://purple-pebble-0eed3e703.6.azurestaticapps.net",
        "https://6juni2026.be",
        "https://www.6juni2026.be/",
        "http://localhost:4200",
        "http://127.0.0.1:4200",
    };

    public static bool TryApply(HttpRequestData req, HttpResponseData res)
    {
        var origin = TryGetHeader(req, "Origin");
        if (origin is null || !AllowedOrigins.Contains(origin))
        {
            return false;
        }

        // Must be set before the response body is written.
        res.Headers.Add("Access-Control-Allow-Origin", origin);
        res.Headers.Add("Access-Control-Allow-Credentials", "true");
        res.Headers.Add("Vary", "Origin");
        return true;
    }

    public static string? TryGetHeader(HttpRequestData req, string headerName)
    {
        return req.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
