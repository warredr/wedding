using Xunit;

namespace wedding_api.Tests;

/// <summary>
/// Marks a test as requiring a running storage emulator (Azurite) via AzureWebJobsStorage.
/// Auto-skips at discovery time when not configured.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        var cs = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(cs))
        {
            Skip = "Integration test requires AzureWebJobsStorage (Azurite) to be set.";
        }
    }
}
