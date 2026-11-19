using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;

public class TextTraceListener : TraceListener, IDisposable
{
    private readonly string _filePath;
    private readonly BlockingCollection<string> _messageQueue;
    private readonly Thread _loggingThread;
    private volatile bool _isDisposed;

    public TextTraceListener(string filePath)
    {
        _filePath = filePath;
        _messageQueue = new BlockingCollection<string>();
        _loggingThread = new Thread(ProcessQueue);
        _loggingThread.IsBackground = true;
        _loggingThread.Start();
    }

    public override void Write(string? message)
    {
        QueueMessage(message);
    }

    public override void WriteLine(string? message)
    {
        QueueMessage(message + Environment.NewLine);
    }

    private void QueueMessage(string? message)
    {
        if (_isDisposed || message == null) return;
        try
        {
            _messageQueue.Add(message);
        }
        catch (InvalidOperationException)
        {
            // Raced with CompleteAdding() during dispose — the message is dropped, which
            // is the correct outcome for a listener that is being torn down.
        }
    }

    private void ProcessQueue()
    {
        foreach (var message in _messageQueue.GetConsumingEnumerable())
        {
            try
            {
                using (var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.Write(message);
                }
            }
            catch
            {
                // A transient log-write failure (locked/rotated file, full disk) must never
                // kill the logging thread — the host process depends on it staying alive.
            }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _messageQueue.CompleteAdding();
            _loggingThread.Join();
            _messageQueue.Dispose();
        }
    }
}