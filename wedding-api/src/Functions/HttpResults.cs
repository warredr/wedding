using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

namespace WeddingApi.Functions;

public static class HttpResults
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static HttpResponseData Json(HttpRequestData req, HttpStatusCode status, object body)
    {
        var res = req.CreateResponse(status);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");

        // Security & caching: these responses can contain auth/session-sensitive data.
        res.Headers.Add("Cache-Control", "no-store");
        res.Headers.Add("Pragma", "no-cache");
        res.Headers.Add("X-Content-Type-Options", "nosniff");

        // CORS headers must be applied before writing the body.
        Cors.TryApply(req, res);

        // In .NET isolated Functions, synchronous response body writes can be disallowed.
        // Use async write and block here to keep the call sites simple.
        res.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions))
            .GetAwaiter()
            .GetResult();
        return res;
    }

    public static HttpResponseData Error(HttpRequestData req, HttpStatusCode status, string code, string message, object? details = null)
    {
        return Json(req, status, new { code, message, details });
    }
}
