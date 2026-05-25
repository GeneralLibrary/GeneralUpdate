using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Event;
using Xunit;

namespace CoreTest.Event;

/// <summary>
/// Batch tests for <see cref="IUpdateEventListener"/> and <see cref="EventManager"/>
/// following AAAT (Arrange-Act-Assert-TearDown).
/// </summary>
public class EventListenerBatchTests : IDisposable
{
    /// <summary>TearDown: clear singleton state after each test for isolation.</summary>
    public void Dispose()
    {
        EventManager.Instance.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IUpdateEventListener_AllMethodsDefined()
    {
        // Arrange & Act
        var listener = new TestListener();

        // Assert
        Assert.NotNull(listener);
        Assert.IsAssignableFrom<IUpdateEventListener>(listener);
    }

    [Fact]
    public void UpdateEventListenerBase_AllDefaultNoOp()
    {
        // Arrange
        var listener = new TestBaseListener();
        var progress = new DownloadProgress("test.zip", 500, 1000, 50.0, DownloadStatus.Downloading);

        // Act — call all methods; base does nothing, so Record.Exception captures any throw
        var ex = Record.Exception(() =>
        {
            listener.OnUpdateInfo(new UpdateInfoEventArgs());
            listener.OnDownloadCompleted(new MultiDownloadCompletedEventArgs("1.0.0", true));
            listener.OnAllDownloadCompleted(new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
            listener.OnDownloadError(new MultiDownloadErrorEventArgs(new System.Exception("e"), "1.0.0"));
            listener.OnDownloadStatistics(new MultiDownloadStatisticsEventArgs("1.0.0", TimeSpan.Zero, "0 B/s", 1000, 500, 50.0));
            listener.OnProgress(new ProgressEventArgs(progress));
            listener.OnException(new ExceptionEventArgs(new System.Exception("test"), "test"));
        });

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void ProgressEventArgs_WrapsDownloadProgress()
    {
        // Arrange
        var progress = new DownloadProgress("test.zip", 500, 1000, 50.0, DownloadStatus.Downloading);

        // Act
        var args = new ProgressEventArgs(progress);

        // Assert
        Assert.Same(progress, args.Progress);
        Assert.Equal("test.zip", args.Progress.AssetName);
        Assert.Equal(500, args.Progress.BytesDownloaded);
        Assert.Equal(50.0, args.Progress.Percentage);
    }

    [Fact]
    public void ExceptionEventArgs_HoldsException()
    {
        // Arrange
        var ex = new System.InvalidOperationException("test error");

        // Act
        var args = new ExceptionEventArgs(ex, "Context message");

        // Assert
        Assert.Same(ex, args.Exception);
        Assert.Equal("Context message", args.Message);
    }

    [Fact]
    public void EventManager_ConcurrentSubscribeUnsubscribe()
    {
        // Arrange
        var manager = EventManager.Instance;
        int callCount = 0;
        void Handler(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref callCount);

        // Act
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

        // Assert — no exceptions thrown during concurrent operations
        Assert.True(true);
    }

    [Fact]
    public void EventManager_DispatchToMultipleListeners()
    {
        // Arrange
        var manager = EventManager.Instance;
        int count1 = 0, count2 = 0;
        void H1(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count1);
        void H2(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count2);

        manager.AddListener<System.EventArgs>(H1);
        manager.AddListener<System.EventArgs>(H2);

        // Act
        manager.Dispatch(this, System.EventArgs.Empty);

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void EventManager_HandlerException_DoesNotBlockOthers()
    {
        // Arrange
        var manager = EventManager.Instance;
        int count = 0;
        void FailingHandler(object? s, System.EventArgs e) => throw new System.InvalidOperationException("handler error");
        void GoodHandler(object? s, System.EventArgs e) => System.Threading.Interlocked.Increment(ref count);

        manager.AddListener<System.EventArgs>(FailingHandler);
        manager.AddListener<System.EventArgs>(GoodHandler);

        // Act
        manager.Dispatch(this, System.EventArgs.Empty);

        // Assert
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
