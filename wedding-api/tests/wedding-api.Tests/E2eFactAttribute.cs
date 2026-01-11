using Xunit;

namespace wedding_api.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class E2eFactAttribute : FactAttribute
{
    public E2eFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("E2E_RUN"), "1", StringComparison.Ordinal))
        {
            Skip = "E2E tests are disabled. Set E2E_RUN=1 to enable.";
            return;
        }

        var storage = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(storage))
        {
            Skip = "E2E tests require AzureWebJobsStorage (Azurite) to be set.";
            return;
        }
    }
}
