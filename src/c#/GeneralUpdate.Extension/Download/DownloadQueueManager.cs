using System.Collections.Concurrent;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Download;

/// <summary>
/// Download queue manager for extensions
/// </summary>
public class DownloadQueueManager : IDownloadQueueManager
{
    private readonly ConcurrentQueue<DownloadTask> _downloadQueue = new();
    private readonly ConcurrentDictionary<string, DownloadTask> _activeTasks = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrentDownloads;
    private bool _isProcessing;
    private readonly CancellationTokenSource _disposalCts = new();
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<DownloadTaskEventArgs>? DownloadStatusChanged;

    /// <summary>
    /// Initialize download queue manager
    /// </summary>
    /// <param name="maxConcurrentDownloads">Maximum concurrent downloads</param>
    public DownloadQueueManager(int maxConcurrentDownloads = 3)
    {
        _maxConcurrentDownloads = maxConcurrentDownloads;
        _semaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);
    }

    /// <inheritdoc/>
    public void Enqueue(DownloadTask task)
    {
        task.Status = ExtensionUpdateStatus.Queued;
        _downloadQueue.Enqueue(task);
        
        _activeTasks[task.Extension.Id] = task;

        OnDownloadStatusChanged(task);

        if (!_isProcessing)
        {
            _ = ProcessQueueAsync();
        }
    }

    /// <inheritdoc/>
    public DownloadTask? GetTask(string extensionId)
    {
        return _activeTasks.TryGetValue(extensionId, out var task) ? task : null;
    }

    /// <inheritdoc/>
    public void CancelTask(string extensionId)
    {
        if (_activeTasks.TryGetValue(extensionId, out var task))
        {
            task.CancellationTokenSource.Cancel();
        }
    }

    /// <inheritdoc/>
    public List<DownloadTask> GetActiveTasks()
    {
        return _activeTasks.Values.ToList();
    }

    private async Task ProcessQueueAsync()
    {
        _isProcessing = true;

        try
        {
            while (true)
            {
                if (!_downloadQueue.TryDequeue(out var task))
                {
                    break;
                }

                if (task != null)
                {
                    await _semaphore.WaitAsync();
                    _ = ProcessTaskAsync(task);
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessTaskAsync(DownloadTask task)
    {
        try
        {
            task.Status = ExtensionUpdateStatus.Updating;
            task.Progress = 0;
            OnDownloadStatusChanged(task);

            // Actual download logic would be injected via callback
            // This is a placeholder for the download process
            await Task.Delay(100, task.CancellationTokenSource.Token);

            task.Status = ExtensionUpdateStatus.UpdateSuccessful;
            task.Progress = 100;
            OnDownloadStatusChanged(task);
        }
        catch (OperationCanceledException)
        {
            task.Status = ExtensionUpdateStatus.UpdateFailed;
            task.ErrorMessage = "Download cancelled";
            OnDownloadStatusChanged(task);
        }
        catch (Exception ex)
        {
            task.Status = ExtensionUpdateStatus.UpdateFailed;
            task.ErrorMessage = ex.Message;
            OnDownloadStatusChanged(task);
        }
        finally
        {
            _semaphore.Release();
            
            if (task.Status == ExtensionUpdateStatus.UpdateSuccessful ||
                task.Status == ExtensionUpdateStatus.UpdateFailed)
            {
                // Schedule cleanup after a delay to allow status checking
                // Use the disposal cancellation token to ensure cleanup on disposal
                _ = Task.Delay(TimeSpan.FromMinutes(5), _disposalCts.Token)
                    .ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            _activeTasks.TryRemove(task.Extension.Id, out var _);
                        }
                    }, TaskScheduler.Default);
            }
        }
    }

    private void OnDownloadStatusChanged(DownloadTask task)
    {
        DownloadStatusChanged?.Invoke(this, new DownloadTaskEventArgs { Task = task });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel all pending cleanup tasks
        _disposalCts.Cancel();

        // Cancel all active download tasks
        foreach (var task in _activeTasks.Values)
        {
            task.CancellationTokenSource.Cancel();
        }
        _activeTasks.Clear();
        
        // Clear queue by dequeuing all items
        while (_downloadQueue.TryDequeue(out _))
        {
            // Just dequeue to clear
        }

        _semaphore.Dispose();
        _disposalCts.Dispose();
    }
}
