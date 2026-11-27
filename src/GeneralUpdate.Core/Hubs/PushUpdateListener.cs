using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Hubs;

internal sealed class PushUpdateListener : IAsyncDisposable
{
    private static int _activeListener;
    private readonly IUpgradeHubService _hub;
    private readonly Func<string, Task<bool>> _handler;
    private readonly Channel<string> _messages;
    private Task? _worker;
    private bool _disposed;

    public Task Completion => _worker ?? Task.CompletedTask;

    public PushUpdateListener(IUpgradeHubService hub, Func<string, Task<bool>> handler, int queueCapacity)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        if (queueCapacity < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacity));

        _messages = Channel.CreateBounded<string>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task StartAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PushUpdateListener));
        if (_worker != null) return;
        if (System.Threading.Interlocked.CompareExchange(ref _activeListener, 1, 0) != 0)
            throw new InvalidOperationException("Only one automatic push listener may own the process-wide storage and IPC at a time.");

        _worker = ProcessMessagesAsync();
        try
        {
            _hub.AddListenerReceive(EnqueueMessage);
            _hub.AddListenerClosed(error =>
            {
                if (error != null)
                    GeneralTracer.Error("PushUpdateListener: hub closed after reconnect attempts.", error);
                _messages.Writer.TryComplete();
                return Task.CompletedTask;
            });
            await _hub.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            _messages.Writer.TryComplete();
            await _worker.ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _messages.Writer.TryComplete();
        if (_worker != null)
            await _worker.ConfigureAwait(false);
        else
            await _hub.DisposeAsync().ConfigureAwait(false);
    }

    private void EnqueueMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 64 * 1024)
        {
            GeneralTracer.Warn("PushUpdateListener: rejected an empty or oversized push message.");
            return;
        }

        if (!_messages.Writer.TryWrite(message))
            GeneralTracer.Warn("PushUpdateListener: push queue is full; the incoming message was rejected.");
    }

    private async Task ProcessMessagesAsync()
    {
        try
        {
            await foreach (var message in _messages.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (await _handler(message).ConfigureAwait(false))
                    {
                        _messages.Writer.TryComplete();
                        GeneralTracer.Info("PushUpdateListener: client update handed to updater; stopping push scheduling.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    GeneralTracer.Error("PushUpdateListener: failed to process a push message.", ex);
                }
            }
        }
        finally
        {
            _messages.Writer.TryComplete();
            try
            {
                await _hub.StopAsync().ConfigureAwait(false);
                await _hub.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _activeListener, 0);
            }
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
