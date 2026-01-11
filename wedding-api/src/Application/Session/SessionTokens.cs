using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WeddingApi.Application.Session;

public static class SessionTokens
{
    private sealed record SessionPayload(long ExpUnixSeconds);

    public static string CreateToken(DateTimeOffset expiresAtUtc, string signingKey)
    {
        var payload = new SessionPayload(ExpUnixSeconds: expiresAtUtc.ToUnixTimeSeconds());
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);

        var signature = Sign(data, signingKey);
        return Base64UrlEncode(data) + "." + Base64UrlEncode(signature);
    }

    public static bool TryValidateToken(string token, string signingKey, DateTimeOffset nowUtc, out DateTimeOffset expiresAtUtc)
    {
        expiresAtUtc = DateTimeOffset.MinValue;

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryBase64UrlDecode(parts[0], out var data) || !TryBase64UrlDecode(parts[1], out var sig))
        {
            return false;
        }

        var expected = Sign(data, signingKey);
        if (!CryptographicOperations.FixedTimeEquals(expected, sig))
        {
            return false;
        }

        SessionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionPayload>(data);
        }
        catch
        {
            return false;
        }

        if (payload is null)
        {
            return false;
        }

        expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(payload.ExpUnixSeconds);
        return expiresAtUtc > nowUtc;
    }

    private static byte[] Sign(byte[] data, string signingKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(data);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string text, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            var padded = text
                .Replace('-', '+')
                .Replace('_', '/');

            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
