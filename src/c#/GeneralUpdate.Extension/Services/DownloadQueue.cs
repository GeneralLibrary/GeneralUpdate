using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Extension.Interfaces;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Manages the download queue for plugin updates.
    /// Integrates with GeneralUpdate.Common.Download.DownloadManager for actual downloads.
    /// </summary>
    public class DownloadQueue : IDownloadQueue
    {
        private readonly Queue<PluginInfo> _queue = new Queue<PluginInfo>();
        private readonly Dictionary<string, UpdateStatus> _statusMap = new Dictionary<string, UpdateStatus>();
        private readonly object _lock = new object();
        private readonly string _downloadPath;
        private readonly int _timeoutSeconds;
        private bool _isRunning;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Event triggered when a plugin enters the Queued status.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginQueued;

        /// <summary>
        /// Event triggered when a plugin enters the Updating status.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginUpdating;

        /// <summary>
        /// Event triggered when a plugin update succeeds.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginUpdateSucceeded;

        /// <summary>
        /// Event triggered when a plugin update fails.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginUpdateFailed;

        /// <summary>
        /// Event triggered to report download/update progress.
        /// </summary>
        public event EventHandler<PluginUpdateEvent> PluginUpdateProgress;

        /// <summary>
        /// Initializes a new instance of DownloadQueue.
        /// </summary>
        /// <param name="downloadPath">Directory path for downloaded files.</param>
        /// <param name="timeoutSeconds">Download timeout in seconds.</param>
        public DownloadQueue(string downloadPath, int timeoutSeconds = 300)
        {
            if (string.IsNullOrWhiteSpace(downloadPath))
                throw new ArgumentException("Download path cannot be null or empty.", nameof(downloadPath));

            _downloadPath = downloadPath;
            _timeoutSeconds = timeoutSeconds;

            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        /// <summary>
        /// Enqueues a plugin for download.
        /// </summary>
        public Task<bool> EnqueueAsync(PluginInfo plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            lock (_lock)
            {
                // Check if already in queue or being processed
                if (_statusMap.ContainsKey(plugin.Id) && 
                    (_statusMap[plugin.Id] == UpdateStatus.Queued || 
                     _statusMap[plugin.Id] == UpdateStatus.Downloading ||
                     _statusMap[plugin.Id] == UpdateStatus.Installing))
                {
                    return Task.FromResult(false);
                }

                _queue.Enqueue(plugin);
                _statusMap[plugin.Id] = UpdateStatus.Queued;
            }

            // Trigger queued event
            OnPluginQueued(plugin);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Removes a plugin from the download queue.
        /// </summary>
        public Task<bool> DequeueAsync(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));

            lock (_lock)
            {
                if (!_statusMap.ContainsKey(pluginId))
                    return Task.FromResult(false);

                // Can only dequeue if it's still in Queued status
                if (_statusMap[pluginId] != UpdateStatus.Queued)
                    return Task.FromResult(false);

                // Remove from queue (this is inefficient but simple)
                var tempList = _queue.ToList();
                var plugin = tempList.FirstOrDefault(p => p.Id == pluginId);
                if (plugin != null)
                {
                    tempList.Remove(plugin);
                    _queue.Clear();
                    foreach (var p in tempList)
                    {
                        _queue.Enqueue(p);
                    }
                }

                _statusMap.Remove(pluginId);
                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Gets the current download status for a plugin.
        /// </summary>
        public UpdateStatus GetStatus(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return UpdateStatus.Idle;

            lock (_lock)
            {
                return _statusMap.TryGetValue(pluginId, out var status) ? status : UpdateStatus.Idle;
            }
        }

        /// <summary>
        /// Gets the number of plugins currently in the queue.
        /// </summary>
        public int GetQueueSize()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }

        /// <summary>
        /// Clears all plugins from the queue.
        /// </summary>
        public Task ClearQueueAsync()
        {
            lock (_lock)
            {
                _queue.Clear();
                _statusMap.Clear();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts processing the download queue.
        /// </summary>
        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning)
                    return Task.CompletedTask;

                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            // Start queue processing in background
            Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops processing the download queue.
        /// </summary>
        public Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning)
                    return Task.CompletedTask;

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            return Task.CompletedTask;
        }

        private async Task ProcessQueueAsync()
        {
            while (_isRunning)
            {
                PluginInfo plugin = null;

                lock (_lock)
                {
                    if (_queue.Count > 0)
                    {
                        plugin = _queue.Dequeue();
                    }
                }

                if (plugin != null)
                {
                    await DownloadPluginAsync(plugin);
                }
                else
                {
                    // No items in queue, wait a bit
                    await Task.Delay(1000);
                }
            }
        }

        private async Task DownloadPluginAsync(PluginInfo plugin)
        {
            try
            {
                // Update status to Downloading
                lock (_lock)
                {
                    _statusMap[plugin.Id] = UpdateStatus.Downloading;
                }
                OnPluginUpdating(plugin);

                if (string.IsNullOrWhiteSpace(plugin.DownloadUrl))
                {
                    throw new InvalidOperationException($"Download URL is not set for plugin {plugin.Id}");
                }

                // Create VersionInfo for DownloadTask
                var versionInfo = new GeneralUpdate.Common.Shared.Object.VersionInfo
                {
                    Name = $"{plugin.Id}-{plugin.AvailableVersion ?? plugin.Version}",
                    Url = plugin.DownloadUrl,
                    Version = plugin.AvailableVersion ?? plugin.Version
                };

                // Create download manager
                var downloadManager = new DownloadManager(_downloadPath, ".zip", _timeoutSeconds);
                
                // Wire up DownloadManager events
                downloadManager.MultiDownloadCompleted += (sender, e) =>
                {
                    // Download of a single task completed
                };

                downloadManager.MultiDownloadError += (sender, e) =>
                {
                    OnDownloadError(plugin, e.Exception);
                };

                downloadManager.MultiDownloadStatistics += (sender, e) =>
                {
                    OnDownloadProgress(plugin, e.BytesReceived, e.TotalBytesToReceive);
                };

                downloadManager.MultiAllDownloadCompleted += (sender, e) =>
                {
                    if (e.IsAllDownloadCompleted)
                    {
                        var fileName = $"{versionInfo.Name}.zip";
                        var downloadPath = Path.Combine(_downloadPath, fileName);
                        OnPluginUpdateSucceeded(plugin, downloadPath);
                    }
                };

                // Create and add download task
                var downloadTask = new DownloadTask(downloadManager, versionInfo);
                downloadManager.Add(downloadTask);

                // Launch download
                await downloadManager.LaunchTasksAsync();

                // Update status to Downloaded/UpdateSucceeded
                lock (_lock)
                {
                    _statusMap[plugin.Id] = UpdateStatus.UpdateSucceeded;
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _statusMap[plugin.Id] = UpdateStatus.UpdateFailed;
                }
                OnPluginUpdateFailed(plugin, ex);
            }
        }

        private void OnPluginQueued(PluginInfo plugin)
        {
            PluginQueued?.Invoke(this, new PluginUpdateEvent
            {
                Plugin = plugin,
                Status = UpdateStatus.Queued,
                Message = $"Plugin {plugin.Name} queued for update"
            });
        }

        private void OnPluginUpdating(PluginInfo plugin)
        {
            PluginUpdating?.Invoke(this, new PluginUpdateEvent
            {
                Plugin = plugin,
                Status = UpdateStatus.Downloading,
                Message = $"Downloading plugin {plugin.Name}"
            });
        }

        private void OnDownloadProgress(PluginInfo plugin, long bytesReceived, long totalBytes)
        {
            var progress = totalBytes > 0 ? (double)bytesReceived / totalBytes * 100 : 0;
            PluginUpdateProgress?.Invoke(this, new PluginUpdateEvent
            {
                Plugin = plugin,
                Status = UpdateStatus.Downloading,
                Progress = progress,
                Message = $"Downloading: {progress:F1}%"
            });
        }

        private void OnDownloadCompleted(PluginInfo plugin, long bytesReceived, long totalBytes)
        {
            // Progress event will be followed by success event
            OnDownloadProgress(plugin, bytesReceived, totalBytes);
        }

        private void OnPluginUpdateSucceeded(PluginInfo plugin, string downloadedPath)
        {
            PluginUpdateSucceeded?.Invoke(this, new PluginUpdateEvent
            {
                Plugin = plugin,
                Status = UpdateStatus.UpdateSucceeded,
                Progress = 100,
                Message = $"Plugin {plugin.Name} downloaded successfully",
                NewVersion = plugin.AvailableVersion
            });
        }

        private void OnPluginUpdateFailed(PluginInfo plugin, Exception exception)
        {
            PluginUpdateFailed?.Invoke(this, new PluginUpdateEvent
            {
                Plugin = plugin,
                Status = UpdateStatus.UpdateFailed,
                Message = $"Failed to download plugin {plugin.Name}: {exception.Message}",
                Exception = exception
            });
        }

        private void OnDownloadError(PluginInfo plugin, Exception exception)
        {
            OnPluginUpdateFailed(plugin, exception);
        }
    }
}
