using System;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore;

internal sealed class SilentVersionPollingTimer : IDisposable
{
    private readonly Func<Task> _pollingAction;
    private readonly Action<Exception> _onError;
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private int _isExecuting;
    private int _isDisposed;
    private readonly ManualResetEventSlim _idleSignal = new(true);
    private static readonly TimeSpan DisposeWaitTimeout = TimeSpan.FromSeconds(2);

    public SilentVersionPollingTimer(Func<Task> pollingAction, TimeSpan interval, Action<Exception> onError)
    {
        _pollingAction = pollingAction;
        _interval = interval;
        _onError = onError;
    }

    public void Start()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            return;
        }

        _timer = new Timer(OnTimerTick, null, _interval, _interval);
    }

    private void OnTimerTick(object? state)
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            return;
        }

        _idleSignal.Reset();
        try
        {
            _ = Task.Run(ExecutePollingAsync);
        }
        catch (Exception exception)
        {
            _onError(exception);
            Volatile.Write(ref _isExecuting, 0);
            _idleSignal.Set();
        }
    }

    private async Task ExecutePollingAsync()
    {
        try
        {
            await _pollingAction();
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
        finally
        {
            Volatile.Write(ref _isExecuting, 0);
            _idleSignal.Set();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        try
        {
            var timer = Interlocked.Exchange(ref _timer, null);
            if (timer == null)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            using var waitHandle = new ManualResetEvent(false);
            timer.Dispose(waitHandle);
            waitHandle.WaitOne(DisposeWaitTimeout);
            _idleSignal.Wait(DisposeWaitTimeout);
        }
        finally
        {
            _idleSignal.Dispose();
        }
    }
}
