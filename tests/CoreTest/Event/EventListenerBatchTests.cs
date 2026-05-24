using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Event;
using Xunit;

namespace CoreTest.Event;

public class EventListenerBatchTests
{
    [Fact]
    public void IUpdateEventListener_AllMethodsDefined()
    {
        var listener = new TestListener();
        Assert.NotNull(listener);
        Assert.IsAssignableFrom<IUpdateEventListener>(listener);
    }

    [Fact]
    public void UpdateEventListenerBase_AllDefaultNoOp()
    {
        var listener = new TestBaseListener();
        var progress = new DownloadProgress("test.zip", 500, 1000, 50.0, DownloadStatus.Downloading);

        listener.OnUpdateInfo(new UpdateInfoEventArgs());
        listener.OnDownloadCompleted(new MultiDownloadCompletedEventArgs("1.0.0", true));
        listener.OnAllDownloadCompleted(new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
        listener.OnDownloadError(new MultiDownloadErrorEventArgs(new System.Exception("e"), "1.0.0"));
        listener.OnDownloadStatistics(new MultiDownloadStatisticsEventArgs("1.0.0", TimeSpan.Zero, "0 B/s", 1000, 500, 50.0));
        listener.OnProgress(new ProgressEventArgs(progress));
        listener.OnException(new ExceptionEventArgs(new System.Exception("test"), "test"));
    }

    [Fact]
    public void ProgressEventArgs_WrapsDownloadProgress()
    {
        var progress = new DownloadProgress("test.zip", 500, 1000, 50.0, DownloadStatus.Downloading);
        var args = new ProgressEventArgs(progress);

        Assert.Same(progress, args.Progress);
        Assert.Equal("test.zip", args.Progress.AssetName);
        Assert.Equal(500, args.Progress.BytesDownloaded);
        Assert.Equal(50.0, args.Progress.Percentage);
    }

    [Fact]
    public void ExceptionEventArgs_HoldsException()
    {
        var ex = new System.InvalidOperationException("test error");
        var args = new ExceptionEventArgs(ex, "Context message");

        Assert.Same(ex, args.Exception);
        Assert.Equal("Context message", args.Message);
    }

    [Fact]
    public void EventManager_ConcurrentSubscribeUnsubscribe()
    {
        var manager = EventManager.Instance;
        int callCount = 0;
        void Handler(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref callCount);

        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(() =>
            {
                if (idx % 2 == 0)
                    manager.AddListener<System.EventArgs>(Handler);
                else
                    manager.RemoveListener<System.EventArgs>(Handler);
            });
        }

        Task.WaitAll(tasks);
    }

    [Fact]
    public void EventManager_DispatchToMultipleListeners()
    {
        var manager = EventManager.Instance;
        int count1 = 0, count2 = 0;
        void H1(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count1);
        void H2(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count2);

        manager.AddListener<System.EventArgs>(H1);
        manager.AddListener<System.EventArgs>(H2);

        try
        {
            manager.Dispatch(this, System.EventArgs.Empty);
        }
        finally
        {
            manager.RemoveListener<System.EventArgs>(H1);
            manager.RemoveListener<System.EventArgs>(H2);
        }

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void EventManager_HandlerException_DoesNotBlockOthers()
    {
        var manager = EventManager.Instance;
        int count = 0;
        void FailingHandler(object? s, System.EventArgs e) => throw new System.InvalidOperationException("handler error");
        void GoodHandler(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count);

        manager.AddListener<System.EventArgs>(FailingHandler);
        manager.AddListener<System.EventArgs>(GoodHandler);

        try
        {
            manager.Dispatch(this, System.EventArgs.Empty);
        }
        finally
        {
            manager.RemoveListener<System.EventArgs>(FailingHandler);
            manager.RemoveListener<System.EventArgs>(GoodHandler);
        }

        Assert.Equal(1, count);
    }

    private class TestListener : IUpdateEventListener
    {
        public void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args) { }
        public void OnDownloadCompleted(MultiDownloadCompletedEventArgs args) { }
        public void OnDownloadError(MultiDownloadErrorEventArgs args) { }
        public void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args) { }
        public void OnUpdateInfo(UpdateInfoEventArgs args) { }
        public void OnException(ExceptionEventArgs args) { }
        public void OnProgress(ProgressEventArgs args) { }
    }

    private class TestBaseListener : UpdateEventListenerBase { }
}
