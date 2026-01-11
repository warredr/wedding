using Microsoft.Azure.Functions.Worker.Http;

namespace WeddingApi.Functions;

public static class Cors
{
    private static readonly HashSet<string> AllowedOrigins = new(StringComparer.Ordinal)
    {
        "https://red-glacier-028bc0303.4.azurestaticapps.net",
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
