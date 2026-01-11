using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace wedding_api.Tests;

internal sealed class FunctionsHostFixture : IAsyncDisposable
{
    private readonly string _workingDirectory;
    private readonly IDictionary<string, string> _environment;
    private Process? _process;

    public Uri BaseUri { get; }

    public FunctionsHostFixture(string workingDirectory, IDictionary<string, string> environment)
    {
        _workingDirectory = workingDirectory;
        _environment = environment;

        var port = GetFreeTcpPort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var funcExe = FindOnPath("func");
        if (funcExe is null)
        {
            throw new InvalidOperationException("Azure Functions Core Tools (func) not found on PATH.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = funcExe,
            Arguments = $"start --port {BaseUri.Port}",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var kv in _environment)
        {
            psi.Environment[kv.Key] = kv.Value;
        }

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start func host.");

        // Wait until host responds.
        using var client = new HttpClient { BaseAddress = BaseUri };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
            {
                var stdout = await _process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderr = await _process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"func host exited early.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            }

            try
            {
                var res = await client.GetAsync("api/config", cancellationToken);
                // Any HTTP response means the host is up (403 is expected without cookie).
                if (res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not ready yet.
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for func host to start.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(cts.Token);
        }
        catch
        {
        }

        _process.Dispose();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string? FindOnPath(string fileName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            var candidate = Path.Combine(p, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Windows not relevant here, but keep compatibility.
            var exe = candidate + ".exe";
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return null;
    }
}
