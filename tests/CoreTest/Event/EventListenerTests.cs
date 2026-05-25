using System;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Event;
using Xunit;

namespace CoreTest.Event;

public class EventListenerTests
{
    private class TestListener : IUpdateEventListener
    {
        public int AllDownloadCompletedCalls;
        public int DownloadCompletedCalls;
        public int DownloadErrorCalls;
        public int DownloadStatisticsCalls;
        public int UpdateInfoCalls;
        public int ExceptionCalls;
        public int ProgressCalls;

        public void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args)
            => AllDownloadCompletedCalls++;

        public void OnDownloadCompleted(MultiDownloadCompletedEventArgs args)
            => DownloadCompletedCalls++;

        public void OnDownloadError(MultiDownloadErrorEventArgs args)
            => DownloadErrorCalls++;

        public void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args)
            => DownloadStatisticsCalls++;

        public void OnUpdateInfo(UpdateInfoEventArgs args)
            => UpdateInfoCalls++;

        public void OnException(ExceptionEventArgs args)
            => ExceptionCalls++;

        public void OnProgress(ProgressEventArgs args)
            => ProgressCalls++;
    }

    [Fact]
    public void EventManager_AddListener_And_Dispatch()
    {
        var listener = new TestListener();
        EventManager.Instance.AddListener<MultiAllDownloadCompletedEventArgs>((s, e) => listener.OnAllDownloadCompleted(e));

        var args = new MultiAllDownloadCompletedEventArgs(true, Array.Empty<(object, string)>());
        EventManager.Instance.Dispatch(this, args);

        Assert.Equal(1, listener.AllDownloadCompletedCalls);
    }

    [Fact]
    public void EventManager_RemoveListener_StopsDispatch()
    {
        var listener = new TestListener();
        Action<object, MultiDownloadCompletedEventArgs> handler = (s, e) => listener.OnDownloadCompleted(e);

        EventManager.Instance.AddListener(handler);
        EventManager.Instance.RemoveListener(handler);

        var args = new MultiDownloadCompletedEventArgs(new object(), true);
        EventManager.Instance.Dispatch(this, args);

        Assert.Equal(0, listener.DownloadCompletedCalls);
    }

    [Fact]
    public void EventManager_MultipleListeners_AllCalled()
    {
        var listener1 = new TestListener();
        var listener2 = new TestListener();

        EventManager.Instance.AddListener<ExceptionEventArgs>((s, e) => listener1.OnException(e));
        EventManager.Instance.AddListener<ExceptionEventArgs>((s, e) => listener2.OnException(e));

        var args = new ExceptionEventArgs(new Exception("test"), "test message");
        EventManager.Instance.Dispatch(this, args);

        Assert.Equal(1, listener1.ExceptionCalls);
        Assert.Equal(1, listener2.ExceptionCalls);
    }

    [Fact]
    public void ProgressEventArgs_Constructor()
    {
        var progress = new DownloadProgress("asset.zip", 500, 1000, 50.0, DownloadStatus.Downloading);
        var args = new ProgressEventArgs(progress);

        Assert.Same(progress, args.Progress);
        Assert.Equal("asset.zip", args.Progress.AssetName);
        Assert.Equal(500, args.Progress.BytesDownloaded);
        Assert.Equal(1000, args.Progress.TotalBytes);
        Assert.Equal(50.0, args.Progress.Percentage);
        Assert.Equal(DownloadStatus.Downloading, args.Progress.Status);
    }

    [Fact]
    public void EventArgs_Types_Constructed()
    {
        var allDone = new MultiAllDownloadCompletedEventArgs(true, Array.Empty<(object, string)>());
        Assert.True(allDone.IsAllDownloadCompleted);

        var downloadDone = new MultiDownloadCompletedEventArgs(new object(), true);
        Assert.NotNull(downloadDone.Version);
        Assert.True(downloadDone.IsCompleted);

        var ex = new Exception("boom");
        var err = new MultiDownloadErrorEventArgs(ex, new object());
        Assert.Same(ex, err.Exception);

        var stats = new MultiDownloadStatisticsEventArgs(new object(), TimeSpan.FromSeconds(1), "1MB/s", 1000, 500, 50.0);
        Assert.Equal("1MB/s", stats.Speed);
        Assert.Equal(1000, stats.TotalBytesToReceive);
    }
}
