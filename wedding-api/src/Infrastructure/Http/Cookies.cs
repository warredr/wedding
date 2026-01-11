namespace WeddingApi.Infrastructure.Http;

public static class Cookies
{
    public static bool TryGetCookieValue(string? cookieHeader, string cookieName, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return false;
        }

        // Very small parser for "a=b; c=d".
        var parts = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var name = part[..idx].Trim();
            if (!string.Equals(name, cookieName, StringComparison.Ordinal))
            {
                continue;
            }

            value = part[(idx + 1)..].Trim();
            return true;
        }

        return false;
    }
}
