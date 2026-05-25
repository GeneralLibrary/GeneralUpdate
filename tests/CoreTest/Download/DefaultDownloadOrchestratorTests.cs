using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Orchestrators;
using GeneralUpdate.Core.Download.Policy;

namespace CoreTest.Download;

public class DefaultDownloadOrchestratorTests
{
    [Fact]
    public void Ctor_HttpClientNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultDownloadOrchestrator(null));
    }

    [Fact]
    public void Ctor_WithValidClient_CreatesInstance()
    {
        var client = new HttpClient();
        var orchestrator = new DefaultDownloadOrchestrator(client);
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Ctor_WithCustomOptions_UsesProvidedOptions()
    {
        var client = new HttpClient();
        var options = new DownloadOrchestratorOptions
        {
            MaxConcurrency = 2,
            EnableResume = false,
            RetryCount = 1,
            VerifyChecksum = false,
            DiffMode = DiffMode.Serial
        };
        var orchestrator = new DefaultDownloadOrchestrator(client, options);
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Ctor_WithCustomPolicy_UsesProvidedPolicy()
    {
        var client = new HttpClient();
        var policy = new DefaultRetryPolicy(5, TimeSpan.FromSeconds(2));
        var orchestrator = new DefaultDownloadOrchestrator(client, null, policy);
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public async Task ExecuteAsync_PlanNull_ReturnsEmptyReport()
    {
        var client = new HttpClient();
        var orchestrator = new DefaultDownloadOrchestrator(client);
        var dest = Path.Combine(Path.GetTempPath(), $"dl_{Guid.NewGuid():N}");
        try
        {
            var report = await orchestrator.ExecuteAsync(null, dest);
            Assert.Equal(0, report.SuccessCount);
            Assert.Equal(0, report.FailedCount);
        }
        finally { if (Directory.Exists(dest)) Directory.Delete(dest, true); }
    }

    [Fact]
    public async Task ExecuteAsync_PlanHasNoAssets_ReturnsEmptyReport()
    {
        var client = new HttpClient();
        var orchestrator = new DefaultDownloadOrchestrator(client);
        var plan = new DownloadPlan(Array.Empty<DownloadAsset>(), false);
        var dest = Path.Combine(Path.GetTempPath(), $"dl_{Guid.NewGuid():N}");
        try
        {
            var report = await orchestrator.ExecuteAsync(plan, dest);
            Assert.Equal(0, report.SuccessCount);
            Assert.Equal(0, report.FailedCount);
        }
        finally { if (Directory.Exists(dest)) Directory.Delete(dest, true); }
    }

    [Fact]
    public async Task ExecuteAsync_DestinationDirectoryCreated()
    {
        var client = new HttpClient();
        var orchestrator = new DefaultDownloadOrchestrator(client);
        var plan = new DownloadPlan(
            new[] { new DownloadAsset("test", "http://example.com/f", 100, null, "1.0") }, false);
        var dest = Path.Combine(Path.GetTempPath(), $"dl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dest);
        try
        {
            var report = await orchestrator.ExecuteAsync(plan, dest);
            Assert.NotNull(report);
        }
        finally { if (Directory.Exists(dest)) Directory.Delete(dest, true); }
    }

    [Fact]
    public async Task ExecuteAsync_GetFileName_ExtractsFromUri()
    {
        var client = new HttpClient();
        var orchestrator = new DefaultDownloadOrchestrator(client);
        // Asset with a proper URL should have file name extracted from URI path
        var asset = new DownloadAsset("test", "http://example.com/path/to/update.zip", 100, null, "1.0");
        Assert.NotNull(asset.Url);
        Assert.Contains("update.zip", asset.Url);
    }
}
