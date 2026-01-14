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
    public const string SessionHeaderName = "X-Rsvp-Session";

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

        if (TryGetValidSessionToken(req, out var token))
        {
            // Token is already validated; we just need to re-parse expiry.
            return SessionTokens.TryValidateToken(token, _options.SessionSigningKey, DateTimeOffset.UtcNow, out expiresAtUtc);
        }

        return false;
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

        // Prefer cookie if present and valid.
        if (TryGetSessionTokenFromCookie(req, out var rawCookieToken) &&
            SessionTokens.TryValidateToken(rawCookieToken, _options.SessionSigningKey, DateTimeOffset.UtcNow, out _))
        {
            token = rawCookieToken;
            return true;
        }

        // Safari iOS can block third-party cookies in XHR/fetch. Support a header-based token as a fallback.
        if (TryGetSessionTokenFromHeader(req, out var rawHeaderToken) &&
            SessionTokens.TryValidateToken(rawHeaderToken, _options.SessionSigningKey, DateTimeOffset.UtcNow, out _))
        {
            token = rawHeaderToken;
            return true;
        }

        // Optional: Authorization: Bearer <token>
        if (TryGetSessionTokenFromAuthorization(req, out var rawBearerToken) &&
            SessionTokens.TryValidateToken(rawBearerToken, _options.SessionSigningKey, DateTimeOffset.UtcNow, out _))
        {
            token = rawBearerToken;
            return true;
        }

        return false;
    }

    private static bool TryGetSessionTokenFromCookie(HttpRequestData req, out string rawToken)
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

    private static bool TryGetSessionTokenFromHeader(HttpRequestData req, out string rawToken)
    {
        rawToken = string.Empty;

        if (!req.Headers.TryGetValues(SessionHeaderName, out var values))
        {
            return false;
        }

        var headerValue = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        rawToken = headerValue.Trim();
        return true;
    }

    private static bool TryGetSessionTokenFromAuthorization(HttpRequestData req, out string rawToken)
    {
        rawToken = string.Empty;

        if (!req.Headers.TryGetValues("Authorization", out var values))
        {
            return false;
        }

        var auth = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = auth[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        rawToken = token;
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
        // Note: behind some proxies/App Service, req.Url.Scheme can show "http" even when the client is on HTTPS.
        var forwardedProto = req.Headers.TryGetValues("X-Forwarded-Proto", out var protoValues)
            ? protoValues.FirstOrDefault()
            : null;
        var hasArrSsl = req.Headers.TryGetValues("X-ARR-SSL", out _);

        var isHttps = string.Equals(req.Url.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase)
                     || hasArrSsl;

        var sameSite = isHttps ? "None" : "Lax";
        var secure = isHttps ? "; Secure" : string.Empty;
        var maxAgeSeconds = Math.Max(0, (int)Math.Ceiling(TimeSpan.FromMinutes(_options.SessionTtlMinutes).TotalSeconds));
        var maxAgeAttr = $"; Max-Age={maxAgeSeconds}";
        var expiresAttr = $"; Expires={expires.UtcDateTime:R}";
        var cookie = $"{SessionCookieName}={token}; Path=/; HttpOnly; SameSite={sameSite}{secure}{maxAgeAttr}{expiresAttr}";
        res.Headers.Add("Set-Cookie", cookie);

        // Include the token as an optional fallback for browsers/environments where third-party cookies are blocked (e.g. Safari iOS).
        // The cookie remains the primary mechanism.
        res.WriteStringAsync(JsonSerializer.Serialize(new { ok = true, expiresAtUtc = expires, token }, JsonOptions))
            .GetAwaiter()
            .GetResult();
        return res;
    }

    public bool IsValidQrKey(string? qrKey)
    {
        if (string.IsNullOrWhiteSpace(qrKey))
        {
            return false;
        }

        return string.Equals(qrKey.Trim(), _options.QrAccessKey, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsValidSixDigitCode(string? code)
    {
        return string.Equals(code, _options.SixDigitAccessCode, StringComparison.Ordinal);
    }
}
