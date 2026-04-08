using System.Collections.Concurrent;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Common.Shared;

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
        GeneralTracer.Info($"DownloadQueueManager: initialized. MaxConcurrentDownloads={maxConcurrentDownloads}");
    }

    /// <inheritdoc/>
    public void Enqueue(DownloadTask task)
    {
        GeneralTracer.Info($"DownloadQueueManager.Enqueue: queuing extension download. ExtensionId={task.Extension.Id}, Name={task.Extension.Name}");
        task.Status = ExtensionUpdateStatus.Queued;
        _downloadQueue.Enqueue(task);
        
        _activeTasks[task.Extension.Id] = task;

        OnDownloadStatusChanged(task);

        if (!_isProcessing)
        {
            GeneralTracer.Info("DownloadQueueManager.Enqueue: starting queue processing.");
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
        GeneralTracer.Info($"DownloadQueueManager.CancelTask: cancelling download. ExtensionId={extensionId}");
        if (_activeTasks.TryGetValue(extensionId, out var task))
        {
            task.CancellationTokenSource.Cancel();
            GeneralTracer.Info($"DownloadQueueManager.CancelTask: cancellation requested. ExtensionId={extensionId}");
        }
        else
        {
            GeneralTracer.Warn($"DownloadQueueManager.CancelTask: task not found in active tasks. ExtensionId={extensionId}");
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
        GeneralTracer.Info("DownloadQueueManager.ProcessQueueAsync: processing download queue.");

        try
        {
            while (true)
            {
                if (!_downloadQueue.TryDequeue(out var task))
                {
                    GeneralTracer.Info("DownloadQueueManager.ProcessQueueAsync: queue is empty, stopping processing.");
                    break;
                }

                if (task != null)
                {
                    GeneralTracer.Debug($"DownloadQueueManager.ProcessQueueAsync: dequeued task for ExtensionId={task.Extension.Id}, waiting for semaphore slot.");
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
        GeneralTracer.Info($"DownloadQueueManager.ProcessTaskAsync: starting download task. ExtensionId={task.Extension.Id}, Name={task.Extension.Name}");
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
            GeneralTracer.Info($"DownloadQueueManager.ProcessTaskAsync: download completed. ExtensionId={task.Extension.Id}");
        }
        catch (OperationCanceledException)
        {
            task.Status = ExtensionUpdateStatus.UpdateFailed;
            task.ErrorMessage = "Download cancelled";
            OnDownloadStatusChanged(task);
            GeneralTracer.Warn($"DownloadQueueManager.ProcessTaskAsync: download cancelled. ExtensionId={task.Extension.Id}");
        }
        catch (Exception ex)
        {
            task.Status = ExtensionUpdateStatus.UpdateFailed;
            task.ErrorMessage = ex.Message;
            OnDownloadStatusChanged(task);
            GeneralTracer.Error($"DownloadQueueManager.ProcessTaskAsync: download failed. ExtensionId={task.Extension.Id}", ex);
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
                            GeneralTracer.Debug($"DownloadQueueManager: cleaned up completed task. ExtensionId={task.Extension.Id}");
                        }
                    }, TaskScheduler.Default);
            }
        }
    }

    private void OnDownloadStatusChanged(DownloadTask task)
    {
        GeneralTracer.Debug($"DownloadQueueManager.OnDownloadStatusChanged: ExtensionId={task.Extension.Id}, Status={task.Status}, Progress={task.Progress}");
        DownloadStatusChanged?.Invoke(this, new DownloadTaskEventArgs { Task = task });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        GeneralTracer.Info($"DownloadQueueManager.Dispose: disposing. ActiveTasks={_activeTasks.Count}, QueuedTasks={_downloadQueue.Count}");
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
        GeneralTracer.Info("DownloadQueueManager.Dispose: all resources released.");
    }
}
