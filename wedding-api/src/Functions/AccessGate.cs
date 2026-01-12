using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using WeddingApi.Application;
using WeddingApi.Application.Session;
using WeddingApi.Infrastructure.Http;

namespace WeddingApi.Functions;

public sealed class AccessGate
{
    public const string SessionCookieName = "rsvp_session";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly RsvpOptions _options;

    public AccessGate(IOptions<RsvpOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAdmin(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var adminKey = query.Get("adminKey");
        return string.Equals(adminKey, _options.AdminKey, StringComparison.Ordinal);
    }

    public bool HasValidSession(HttpRequestData req)
    {
        return TryGetValidSessionToken(req, out _);
    }

    public bool TryGetSessionExpiresAtUtc(HttpRequestData req, out DateTimeOffset expiresAtUtc)
    {
        expiresAtUtc = DateTimeOffset.MinValue;

        if (!TryGetSessionToken(req, out var rawToken))
        {
            return false;
        }

        return SessionTokens.TryValidateToken(rawToken, _options.SessionSigningKey, DateTimeOffset.UtcNow, out expiresAtUtc);
    }

    public bool TryGetDeviceId(HttpRequestData req, out string deviceId)
    {
        // We don't want to expose or persist the raw session token.
        // Instead, derive a stable, non-reversible device identifier via SHA-256.
        if (!TryGetValidSessionToken(req, out var token))
        {
            deviceId = string.Empty;
            return false;
        }

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        deviceId = Convert.ToHexString(hash).ToLowerInvariant();
        return true;
    }

    private bool TryGetValidSessionToken(HttpRequestData req, out string token)
    {
        token = string.Empty;

        if (!TryGetSessionToken(req, out var rawToken))
        {
            return false;
        }

        if (!SessionTokens.TryValidateToken(rawToken, _options.SessionSigningKey, DateTimeOffset.UtcNow, out _))
        {
            return false;
        }

        token = rawToken;
        return true;
    }

    private static bool TryGetSessionToken(HttpRequestData req, out string rawToken)
    {
        rawToken = string.Empty;

        if (!req.Headers.TryGetValues("Cookie", out var values))
        {
            return false;
        }

        var cookieHeader = values.FirstOrDefault();
        if (!Cookies.TryGetCookieValue(cookieHeader, SessionCookieName, out rawToken))
        {
            return false;
        }

        return true;
    }

    public HttpResponseData? RequireSession(HttpRequestData req)
    {
        return HasValidSession(req)
            ? null
            : HttpResults.Error(req, HttpStatusCode.Forbidden, "unauthorized", "Session required.");
    }

    public HttpResponseData IssueSessionCookie(HttpRequestData req)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.SessionTtlMinutes);
        var token = SessionTokens.CreateToken(expires, _options.SessionSigningKey);

        // Important: set headers (especially Set-Cookie) BEFORE writing the body.
        // In some hosting modes, writing the body can commit headers.
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");

        // CORS headers must be applied before writing the body.
        Cors.TryApply(req, res);

        // Cookies:
        // - Prod (Static Web App -> Functions host): cross-site; requires SameSite=None and Secure.
        var isHttps = string.Equals(req.Url.Scheme, "https", StringComparison.OrdinalIgnoreCase);

        var sameSite = isHttps ? "None" : "Lax";
        var secure = isHttps ? "; Secure" : string.Empty;
        var expiresAttr = $"; Expires={expires.UtcDateTime:R}";
        var cookie = $"{SessionCookieName}={token}; Path=/; HttpOnly; SameSite={sameSite}{secure}{expiresAttr}";
        res.Headers.Add("Set-Cookie", cookie);

        res.WriteStringAsync(JsonSerializer.Serialize(new { ok = true, expiresAtUtc = expires }, JsonOptions))
            .GetAwaiter()
            .GetResult();
        return res;
    }

    public bool IsValidQrKey(string? qrKey)
    {
        return string.Equals(qrKey, _options.QrAccessKey, StringComparison.Ordinal);
    }

    public bool IsValidSixDigitCode(string? code)
    {
        return string.Equals(code, _options.SixDigitAccessCode, StringComparison.Ordinal);
    }
}
