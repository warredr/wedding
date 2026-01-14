using System.Net;
using Microsoft.Extensions.Options;
using WeddingApi.Application;
using WeddingApi.Application.Session;
using WeddingApi.Functions;

namespace wedding_api.Tests;

public sealed class AccessGateTests
{
    private static AccessGate CreateSut()
    {
        var options = new RsvpOptions
        {
            QrAccessKey = "test-qr",
            SixDigitAccessCode = "123456",
            AdminKey = "test-admin",
            SessionSigningKey = "test-signing-key-123",
            SessionTtlMinutes = 60
        };
        return new AccessGate(Options.Create(options));
    }

    [Fact]
    public void IssueSessionCookie_Http_SetsLax()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/start"), "POST");

        var res = sut.IssueSessionCookie(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var values));
        var cookie = values.First();
        Assert.Contains("SameSite=Lax", cookie);
        Assert.DoesNotContain("Secure", cookie);
    }

    [Fact]
    public void IssueSessionCookie_Https_SetsNoneSecure()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("https://localhost/api/session/start"), "POST");

        var res = sut.IssueSessionCookie(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var values));
        var cookie = values.First();
        Assert.Contains("SameSite=None", cookie);
        Assert.Contains("Secure", cookie);
    }

    [Fact]
    public void IssueSessionCookie_HttpWithForwardedHttps_SetsNoneSecure()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/start"), "POST");
        req.Headers.Add("X-Forwarded-Proto", "https");

        var res = sut.IssueSessionCookie(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var values));
        var cookie = values.First();
        Assert.Contains("SameSite=None", cookie);
        Assert.Contains("Secure", cookie);
    }

    [Fact]
    public void IssueSessionCookie_HttpWithForwardedHttp_SetsLax()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("http://localhost/api/session/start"), "POST");
        req.Headers.Add("X-Forwarded-Proto", "http");

        var res = sut.IssueSessionCookie(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var values));
        var cookie = values.First();
        Assert.Contains("SameSite=Lax", cookie);
        Assert.DoesNotContain("Secure", cookie);
    }

    [Fact]
    public void HasValidSession_HeaderToken_IsAccepted()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("https://localhost/api/search"), "GET");

        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var token = SessionTokens.CreateToken(expires, "test-signing-key-123");
        req.Headers.Add(AccessGate.SessionHeaderName, token);

        Assert.True(sut.HasValidSession(req));
        Assert.True(sut.TryGetSessionExpiresAtUtc(req, out var expiresAtUtc));
        Assert.True(expiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void HasValidSession_InvalidHeaderToken_DoesNotOverrideValidCookie()
    {
        var sut = CreateSut();
        var ctx = new TestFunctionContext();
        var req = new TestHttpRequestData(ctx, new Uri("https://localhost/api/search"), "GET");

        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var validToken = SessionTokens.CreateToken(expires, "test-signing-key-123");

        // Cookie header contains a valid session.
        req.Headers.Add("Cookie", $"{AccessGate.SessionCookieName}={validToken}");
        // Header contains garbage.
        req.Headers.Add(AccessGate.SessionHeaderName, "not-a-valid-token");

        Assert.True(sut.HasValidSession(req));
    }
}
