using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Event;
using Xunit;

namespace CoreTest.Event;

[Collection("NonParallel_EventManager")]
public class EventManagerConcurrencyTests : IDisposable
{
    /// <summary>TearDown: clear singleton state after each test for isolation.</summary>
    public void Dispose()
    {
        EventManager.Instance.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ConcurrentAddRemoveDispatch_NoExceptions()
    {
        var completed = new TaskCompletionSource<bool>();
        var errors = 0;

        // Concurrent writers
        Parallel.For(0, 100, i =>
        {
            try
            {
                Action<object, ExceptionEventArgs> handler = (s, e) => { };
                EventManager.Instance.AddListener(handler);
                EventManager.Instance.RemoveListener(handler);
            }
            catch { Interlocked.Increment(ref errors); }
        });

        // Concurrent dispatchers
        Parallel.For(0, 50, i =>
        {
            try
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(new Exception("test"), "test"));
            }
            catch { Interlocked.Increment(ref errors); }
        });

        // Concurrent readers (dispatch + add)
        Parallel.For(0, 50, i =>
        {
            try
            {
                Action<object, ProgressEventArgs> handler = (s, e) => { };
                EventManager.Instance.AddListener(handler);
                EventManager.Instance.RemoveListener(handler);
            }
            catch { Interlocked.Increment(ref errors); }
        });

        await Task.Delay(100); // Let all tasks finish
        Assert.Equal(0, errors);
    }

    [Fact]
    public void Dispatch_HandlerException_DoesNotBlockOthers()
    {
        var handler2Called = false;
        Action<object, ExceptionEventArgs> handler1 = (s, e) => throw new InvalidOperationException("boom");
        Action<object, ExceptionEventArgs> handler2 = (s, e) => handler2Called = true;

        EventManager.Instance.AddListener(handler1);
        EventManager.Instance.AddListener(handler2);

        EventManager.Instance.Dispatch(this, new ExceptionEventArgs(null, "test"));

        Assert.True(handler2Called);


    }

    [Fact]
    public void AddRemove_Dispatch_DoesNotThrow()
    {
        var callCount = 0;
        Action<object, ExceptionEventArgs> handler = (s, e) => Interlocked.Increment(ref callCount);

        EventManager.Instance.AddListener(handler);
        EventManager.Instance.Dispatch(this, new ExceptionEventArgs(null, "test"));
        EventManager.Instance.RemoveListener(handler);
        EventManager.Instance.Dispatch(this, new ExceptionEventArgs(null, "test"));

        Assert.Equal(1, callCount);
    }
}
