using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Progress;
using GeneralUpdate.Core.Event;

namespace CoreTest.Download;

[Collection("NonParallel_EventManager")]
public class DownloadProgressReporterTests : IDisposable
{
    public void Dispose()
    {
        EventManager.Instance.Clear();
    }

    #region Progress callback

    [Theory]
    [InlineData(1000L, 50.0, DownloadStatus.Downloading)]
    [InlineData(null, -1.0, DownloadStatus.Downloading)]
    [InlineData(500L, 100.0, DownloadStatus.Completed)]
    public void Report_DispatchesStatisticsAlongsideProgress(long? totalBytes, double percentage, DownloadStatus status)
    {
        MultiDownloadStatisticsEventArgs? statistics = null;
        ProgressEventArgs? progressArgs = null;
        var completedCount = 0;
        new GeneralUpdate.Core.GeneralUpdateBootstrap()
            .AddListenerMultiDownloadStatistics((_, args) => statistics = args)
            .AddListenerProgress((_, args) => progressArgs = args)
            .AddListenerMultiDownloadCompleted((_, _) => completedCount++);
        var progress = new DownloadProgress("asset.zip", 500, totalBytes, percentage, status);

        DownloadProgressReporter.CreateEventBridge().Report(progress);

        Assert.NotNull(statistics);
        Assert.Equal("asset.zip", statistics.Version);
        Assert.Equal(totalBytes ?? 0, statistics.TotalBytesToReceive);
        Assert.Equal(500, statistics.BytesReceived);
        Assert.Equal(percentage, statistics.ProgressPercentage);
        Assert.Same(progress, progressArgs?.Progress);
        Assert.Equal(status == DownloadStatus.Completed ? 1 : 0, completedCount);
    }

    [Fact]
    public void Report_InvokesOnProgressCallback()
    {
        DownloadProgress? captured = null;
        var reporter = new DownloadProgressReporter(onProgress: p => captured = p);

        var progress = new DownloadProgress("asset.zip", 500, 1000, 50.0, DownloadStatus.Downloading);
        reporter.Report(progress);

        Assert.NotNull(captured);
        Assert.Equal("asset.zip", captured!.AssetName);
        Assert.Equal(500, captured.BytesDownloaded);
        Assert.Equal(1000, captured.TotalBytes);
        Assert.Equal(50.0, captured.Percentage);
        Assert.Equal(DownloadStatus.Downloading, captured.Status);
    }

    [Fact]
    public void Report_NullProgressCallback_DoesNotThrow()
    {
        var reporter = new DownloadProgressReporter(onProgress: null);

        var progress = new DownloadProgress("a.zip", 0, 100, 0, DownloadStatus.Pending);
        var ex = Record.Exception(() => reporter.Report(progress));

        Assert.Null(ex);
    }

    #endregion

    #region Completed callback

    [Fact]
    public void Report_CompletedStatus_InvokesOnCompleted()
    {
        var completedInvoked = false;
        var reporter = new DownloadProgressReporter(onProgress: null, onCompleted: () => completedInvoked = true);

        var progress = new DownloadProgress("done.zip", 1000, 1000, 100.0, DownloadStatus.Completed);
        reporter.Report(progress);

        Assert.True(completedInvoked);
    }

    [Fact]
    public void Report_NonCompletedStatus_DoesNotInvokeOnCompleted()
    {
        var completedInvoked = false;
        var reporter = new DownloadProgressReporter(onProgress: null, onCompleted: () => completedInvoked = true);

        var progress = new DownloadProgress("notdone.zip", 500, 1000, 50.0, DownloadStatus.Downloading);
        reporter.Report(progress);

        Assert.False(completedInvoked);
    }

    [Fact]
    public void Report_CompletedStatus_NullOnCompleted_DoesNotThrow()
    {
        var reporter = new DownloadProgressReporter(onProgress: null, onCompleted: null);

        var progress = new DownloadProgress("done.zip", 1000, 1000, 100.0, DownloadStatus.Completed);
        var ex = Record.Exception(() => reporter.Report(progress));

        Assert.Null(ex);
    }

    [Fact]
    public void Report_FailedStatus_DoesNotInvokeOnCompleted()
    {
        var completedInvoked = false;
        var reporter = new DownloadProgressReporter(onProgress: null, onCompleted: () => completedInvoked = true);

        var progress = new DownloadProgress("fail.zip", 0, 100, 0, DownloadStatus.Failed);
        reporter.Report(progress);

        Assert.False(completedInvoked);
    }

    #endregion

    #region Event dispatch on Completed

    [Fact]
    public void Report_CompletedStatus_DispatchesCompletedEvent()
    {
        MultiDownloadCompletedEventArgs? captured = null;
        Action<object, MultiDownloadCompletedEventArgs> handler = (_, args) => captured = args;
        EventManager.Instance.AddListener<MultiDownloadCompletedEventArgs>(handler);

        try
        {
            var reporter = new DownloadProgressReporter();
            reporter.Report(new DownloadProgress("asset.zip", 1000, 1000, 100.0, DownloadStatus.Completed));

            Assert.NotNull(captured);
            Assert.Equal("asset.zip", captured!.Version); // Version is set to the asset name
            Assert.True(captured.IsCompleted);
        }
        finally
        {
            EventManager.Instance.RemoveListener<MultiDownloadCompletedEventArgs>(handler);
        }
    }

    #endregion

    #region Event dispatch on Failed

    [Fact]
    public void Report_FailedStatus_DispatchesErrorEvent()
    {
        MultiDownloadErrorEventArgs? captured = null;
        Action<object, MultiDownloadErrorEventArgs> handler = (_, args) => captured = args;
        EventManager.Instance.AddListener<MultiDownloadErrorEventArgs>(handler);

        try
        {
            var reporter = new DownloadProgressReporter();
            reporter.Report(new DownloadProgress("fail.zip", 0, 100, 0, DownloadStatus.Failed));

            Assert.NotNull(captured);
            Assert.Equal("fail.zip", captured!.Version);
            Assert.NotNull(captured.Exception);
        }
        finally
        {
            EventManager.Instance.RemoveListener<MultiDownloadErrorEventArgs>(handler);
        }
    }

    #endregion

    #region DispatchAllCompleted

    [Fact]
    public void DispatchAllCompleted_Success_DispatchesEvent()
    {
        MultiAllDownloadCompletedEventArgs? captured = null;
        Action<object, MultiAllDownloadCompletedEventArgs> handler = (_, args) => captured = args;
        EventManager.Instance.AddListener<MultiAllDownloadCompletedEventArgs>(handler);

        try
        {
            DownloadProgressReporter.DispatchAllCompleted(this, true, null!);

            Assert.NotNull(captured);
            Assert.True(captured!.IsAllDownloadCompleted);
        }
        finally
        {
            EventManager.Instance.RemoveListener<MultiAllDownloadCompletedEventArgs>(handler);
        }
    }

    [Fact]
    public void DispatchAllCompleted_Failure_EventHasFalseFlag()
    {
        MultiAllDownloadCompletedEventArgs? captured = null;
        Action<object, MultiAllDownloadCompletedEventArgs> handler = (_, args) => captured = args;
        EventManager.Instance.AddListener<MultiAllDownloadCompletedEventArgs>(handler);

        try
        {
            DownloadProgressReporter.DispatchAllCompleted(this, false, new List<(object, string)>());

            Assert.NotNull(captured);
            Assert.False(captured!.IsAllDownloadCompleted);
        }
        finally
        {
            EventManager.Instance.RemoveListener<MultiAllDownloadCompletedEventArgs>(handler);
        }
    }

    #endregion

    #region CreateEventBridge

    [Fact]
    public void CreateEventBridge_ReturnsNonNull()
    {
        var progress = DownloadProgressReporter.CreateEventBridge();
        Assert.NotNull(progress);
        Assert.IsAssignableFrom<System.IProgress<DownloadProgress>>(progress);
    }

    [Fact]
    public void CreateEventBridge_Reporting_DoesNotThrow()
    {
        var progress = DownloadProgressReporter.CreateEventBridge();

        var dp = new DownloadProgress("bridge.zip", 500, 1000, 50.0, DownloadStatus.Downloading);
        var ex = Record.Exception(() => progress.Report(dp));

        Assert.Null(ex);
    }

    #endregion

    #region All download statuses

    [Theory]
    [InlineData(DownloadStatus.Pending)]
    [InlineData(DownloadStatus.Downloading)]
    [InlineData(DownloadStatus.Retrying)]
    public void Report_NonTerminalStatuses_ReportProgress(DownloadStatus status)
    {
        DownloadProgress? captured = null;
        var reporter = new DownloadProgressReporter(onProgress: p => captured = p);

        var progress = new DownloadProgress("a.zip", 100, 1000, 10.0, status);
        reporter.Report(progress);

        Assert.NotNull(captured);
        Assert.Equal(status, captured!.Status);
    }

    #endregion

    #region AssetName null/empty cases

    [Fact]
    public void Report_AssetNameNull_DoesNotThrow()
    {
        var reporter = new DownloadProgressReporter();
        var progress = new DownloadProgress(null, 0, null, 0, DownloadStatus.Pending);

        var ex = Record.Exception(() => reporter.Report(progress));

        Assert.Null(ex);
    }

    [Fact]
    public void Report_AssetNameNull_Completed_UsesUnknown()
    {
        MultiDownloadCompletedEventArgs? captured = null;
        Action<object, MultiDownloadCompletedEventArgs> handler = (_, args) => captured = args;
        EventManager.Instance.AddListener<MultiDownloadCompletedEventArgs>(handler);

        try
        {
            var reporter = new DownloadProgressReporter();
            reporter.Report(new DownloadProgress(null, 100, 100, 100.0, DownloadStatus.Completed));

            Assert.NotNull(captured);
            Assert.Equal("unknown", captured!.Version);
        }
        finally
        {
            EventManager.Instance.RemoveListener<MultiDownloadCompletedEventArgs>(handler);
        }
    }

    #endregion
}
