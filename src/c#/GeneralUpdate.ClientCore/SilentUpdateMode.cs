using System;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore;

internal sealed class SilentUpdateMode : IDisposable
{
    private readonly TimeSpan _pollingInterval;
    private readonly Func<Task> _pollingAction;
    private readonly Func<Task> _prepareAction;
    private readonly Action _launchOnExitAction;
    private readonly Action<Exception> _onError;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private int _isPrepared;
    private int _isPollingStarted;
    private int _isDisposed;
    private int _isLaunchTriggered;
    private int _isExitHookRegistered;

    public SilentUpdateMode(
        TimeSpan pollingInterval,
        Func<Task> pollingAction,
        Func<Task> prepareAction,
        Action launchOnExitAction,
        Action<Exception> onError)
    {
        _pollingInterval = pollingInterval;
        _pollingAction = pollingAction;
        _prepareAction = prepareAction;
        _launchOnExitAction = launchOnExitAction;
        _onError = onError;
    }

    public async Task EnterAsync(bool hasUpdate)
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            return;
        }

        if (hasUpdate)
        {
            await PrepareAndScheduleAsync();
            return;
        }

        StartPolling();
    }

    private async Task PrepareAndScheduleAsync()
    {
        if (Interlocked.CompareExchange(ref _isPrepared, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await _prepareAction();
            StopPolling();
            RegisterExitHook();
        }
        catch
        {
            Interlocked.Exchange(ref _isPrepared, 0);
            throw;
        }
    }

    private void RegisterExitHook()
    {
        if (Interlocked.CompareExchange(ref _isExitHookRegistered, 1, 0) == 0)
        {
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainProcessExit;
        }
    }

    private void UnregisterExitHook()
    {
        if (Interlocked.CompareExchange(ref _isExitHookRegistered, 0, 1) == 1)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnCurrentDomainProcessExit;
        }
    }

    private void OnCurrentDomainProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _isPrepared) == 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isLaunchTriggered, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _launchOnExitAction();
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
    }

    private void StartPolling()
    {
        if (Volatile.Read(ref _isPrepared) == 1 ||
            Volatile.Read(ref _isDisposed) == 1 ||
            Interlocked.CompareExchange(ref _isPollingStarted, 1, 0) != 0)
        {
            return;
        }

        _pollingCts = new CancellationTokenSource();
        _pollingTask = RunPollingLoopAsync(_pollingCts.Token);
    }

    private void StopPolling()
    {
        var cts = Interlocked.Exchange(ref _pollingCts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _isPrepared) == 0)
            {
                await _pollingAction();
                await Task.Delay(_pollingInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation.
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
        finally
        {
            Interlocked.Exchange(ref _isPollingStarted, 0);
            _pollingTask = null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        StopPolling();
        UnregisterExitHook();
    }
}
