using System.Collections.Concurrent;
using System.Reflection;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Orchestrators;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Strategy;
using Moq;

namespace CoreTest.Strategy;

[Collection("NonParallel_EventManager")]
public class DownloadEventTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GU_DownloadEvents_{Guid.NewGuid():N}");
    private readonly ConcurrentQueue<MultiDownloadStatisticsEventArgs> _statistics = new();
    private readonly ConcurrentQueue<MultiDownloadCompletedEventArgs> _completed = new();
    private readonly ConcurrentQueue<MultiAllDownloadCompletedEventArgs> _allCompleted = new();
    private readonly IBlackMatcher? _blackMatcher = StorageManager.BlackMatcher;

    public DownloadEventTests()
    {
        Directory.CreateDirectory(_directory);
        new GeneralUpdateBootstrap()
            .AddListenerMultiDownloadStatistics((_, args) => _statistics.Enqueue(args))
            .AddListenerMultiDownloadCompleted((_, args) => _completed.Enqueue(args))
            .AddListenerMultiAllDownloadCompleted((_, args) => _allCompleted.Enqueue(args));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientDownload_DefaultAndInjectedOrchestrators_NotifyListeners(bool injectOrchestrator)
    {
        var asset = new DownloadAsset("update.zip", "https://example.com/update.zip",
            1000, null, "2.0.0", AppType: (int)AppType.Upgrade);
        var source = new Mock<IDownloadSource>();
        source.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadSourceResult { Assets = new[] { asset }, HasUpgradeUpdate = true });
        var executor = CreateExecutor();
        using var httpClient = new HttpClient();
        var strategy = new ClientStrategy { DownloadSource = source.Object };
        strategy.SetOsStrategy(Mock.Of<IStrategy>());
        if (injectOrchestrator)
            strategy.SetOrchestrator(new DefaultDownloadOrchestrator(httpClient,
                new DownloadOrchestratorOptions { VerifyChecksum = false }, executor: executor.Object));
        else
            strategy.SetDownloadExecutor(executor.Object);
        var context = new UpdateContext
        {
            UpdateUrl = "https://example.com/update",
            ClientVersion = "1.0.0",
            UpgradeClientVersion = "1.0.0",
            MainAppName = "MainApp",
            InstallPath = _directory,
            BackupEnabled = false,
            VerifyChecksum = false
        };
        strategy.Create(context);

        await strategy.ExecuteAsync();

        AssertDownloadEvents();
    }

    [Fact]
    public async Task OssDownload_InjectedOrchestrator_NotifiesListeners()
    {
        var asset = new DownloadAsset("update.zip", "https://example.com/update.zip", 1000, null, "2.0.0");
        using var httpClient = new HttpClient();
        var strategy = new OssStrategy(AppType.OssUpgrade)
        {
            DownloadOrchestrator = new DefaultDownloadOrchestrator(httpClient,
                new DownloadOrchestratorOptions { VerifyChecksum = false }, executor: CreateExecutor().Object)
        };
        // Exercise the download phase without launching applications or exiting the test process.
        var download = typeof(OssStrategy).GetMethod("DownloadAssetsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)download.Invoke(strategy, new object[] { new List<DownloadAsset> { asset }, _directory })!;

        AssertDownloadEvents();
    }

    private static Mock<IDownloadExecutor> CreateExecutor()
    {
        var executor = new Mock<IDownloadExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<DownloadAsset>(), It.IsAny<string>(),
                It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()))
            .Returns((DownloadAsset asset, string path, IProgress<DownloadProgress>? progress, CancellationToken _) =>
            {
                progress?.Report(new DownloadProgress(asset.Name, 500, 1000, 50, DownloadStatus.Downloading));
                progress?.Report(new DownloadProgress(asset.Name, 1000, 1000, 100, DownloadStatus.Completed));
                return Task.FromResult(new DownloadResult(asset, path, 1000, TimeSpan.Zero, 0, true, null));
            });
        return executor;
    }

    private void AssertDownloadEvents()
    {
        var completed = Assert.Single(_completed);
        Assert.True(completed.IsCompleted);
        Assert.Equal("update.zip", completed.Version);
        Assert.Contains(_statistics, s => s.BytesReceived == 500 && s.ProgressPercentage == 50);
        Assert.Contains(_statistics, s => s.BytesReceived == 1000 && s.ProgressPercentage == 100);
        Assert.True(Assert.Single(_allCompleted).IsAllDownloadCompleted);
    }

    public void Dispose()
    {
        EventManager.Instance.Clear();
        StorageManager.BlackMatcher = _blackMatcher;
        Directory.Delete(_directory, true);
    }
}
