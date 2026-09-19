using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.Strategy;

public class ClientStrategyUpdateLogTests
{
    [Fact]
    public async Task ExecuteAsync_PrecheckReceivesUpdateLog()
    {
        var updateLog = "# 2.0.0\n- Preserve release notes";
        UpdateInfoEventArgs? captured = null;
        var strategy = new ClientStrategy
        {
            DownloadSource = new StubDownloadSource(new DownloadAsset(
                Name: "package.zip",
                Url: "https://cdn.example.com/package.zip",
                Size: 1024,
                SHA256: "hash",
                Version: "2.0.0",
                AppType: (int)AppType.Client)
            {
                UpdateLog = updateLog
            })
        };

        strategy.UseUpdatePrecheck(args =>
        {
            captured = args;
            return true;
        });
        strategy.Create(new UpdateContext
        {
            UpdateUrl = "https://api.example.com/update",
            ClientVersion = "1.0.0",
            AppSecretKey = "key",
            MainAppName = "MainApp",
            InstallPath = Path.GetTempPath()
        });

        await strategy.ExecuteAsync();

        Assert.NotNull(captured);
        var version = Assert.Single(captured.Info!.Body!);
        Assert.Equal(updateLog, version.UpdateLog);
    }

    private sealed class StubDownloadSource(DownloadAsset asset) : IDownloadSource
    {
        public Task<DownloadSourceResult> ListAsync(CancellationToken token = default)
            => Task.FromResult(new DownloadSourceResult
            {
                Assets = new[] { asset },
                HasMainUpdate = true
            });
    }
}
