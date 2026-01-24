using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Extension.Events;
using GeneralUpdate.Extension.Models;
using GeneralUpdate.Extension.Queue;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Handles downloading of extensions using DownloadManager.
    /// </summary>
    public class ExtensionDownloader
    {
        private readonly string _downloadPath;
        private readonly int _downloadTimeout;
        private readonly ExtensionUpdateQueue _updateQueue;

        /// <summary>
        /// Event fired when download progress updates.
        /// </summary>
        public event EventHandler<ExtensionDownloadProgressEventArgs>? DownloadProgress;

        /// <summary>
        /// Event fired when a download completes.
        /// </summary>
        public event EventHandler<ExtensionEventArgs>? DownloadCompleted;

        /// <summary>
        /// Event fired when a download fails.
        /// </summary>
        public event EventHandler<ExtensionEventArgs>? DownloadFailed;

        /// <summary>
        /// Initializes a new instance of the ExtensionDownloader.
        /// </summary>
        /// <param name="downloadPath">Path where extensions will be downloaded.</param>
        /// <param name="updateQueue">The update queue to manage.</param>
        /// <param name="downloadTimeout">Download timeout in seconds.</param>
        public ExtensionDownloader(string downloadPath, ExtensionUpdateQueue updateQueue, int downloadTimeout = 300)
        {
            _downloadPath = downloadPath ?? throw new ArgumentNullException(nameof(downloadPath));
            _updateQueue = updateQueue ?? throw new ArgumentNullException(nameof(updateQueue));
            _downloadTimeout = downloadTimeout;

            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        /// <summary>
        /// Downloads an extension.
        /// </summary>
        /// <param name="queueItem">The queue item to download.</param>
        /// <returns>Path to the downloaded file, or null if download failed.</returns>
        public async Task<string?> DownloadExtensionAsync(ExtensionUpdateQueueItem queueItem)
        {
            if (queueItem == null)
                throw new ArgumentNullException(nameof(queueItem));

            var extension = queueItem.Extension;
            var metadata = extension.Metadata;

            if (string.IsNullOrWhiteSpace(metadata.DownloadUrl))
            {
                _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, "Download URL is missing");
                OnDownloadFailed(metadata.Id, metadata.Name);
                return null;
            }

            try
            {
                _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.Updating);

                // Determine file format
                var format = !string.IsNullOrWhiteSpace(metadata.DownloadUrl) && metadata.DownloadUrl!.Contains(".")
                    ? Path.GetExtension(metadata.DownloadUrl)
                    : ".zip";

                // Create VersionInfo for DownloadManager
                var versionInfo = new VersionInfo
                {
                    Name = $"{metadata.Id}_{metadata.Version}",
                    Url = metadata.DownloadUrl,
                    Hash = metadata.Hash,
                    Version = metadata.Version,
                    Size = metadata.Size,
                    Format = format
                };

                // Create DownloadManager instance
                var downloadManager = new DownloadManager(_downloadPath, format, _downloadTimeout);

                // Subscribe to events
                downloadManager.MultiDownloadStatistics += (sender, args) => OnDownloadStatistics(queueItem, args);
                downloadManager.MultiDownloadCompleted += (sender, args) => OnMultiDownloadCompleted(queueItem, args);
                downloadManager.MultiDownloadError += (sender, args) => OnMultiDownloadError(queueItem, args);

                // Create download task and add to manager
                var downloadTask = new DownloadTask(downloadManager, versionInfo);
                downloadManager.Add(downloadTask);

                // Launch download
                await downloadManager.LaunchTasksAsync();

                var downloadedFilePath = Path.Combine(_downloadPath, $"{versionInfo.Name}{format}");

                if (File.Exists(downloadedFilePath))
                {
                    _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateSuccessful);
                    OnDownloadCompleted(metadata.Id, metadata.Name);
                    return downloadedFilePath;
                }
                else
                {
                    _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, "Downloaded file not found");
                    OnDownloadFailed(metadata.Id, metadata.Name);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, ex.Message);
                OnDownloadFailed(metadata.Id, metadata.Name);
                return null;
            }
        }

        private void OnDownloadStatistics(ExtensionUpdateQueueItem queueItem, MultiDownloadStatisticsEventArgs args)
        {
            var progress = args.ProgressPercentage;
            _updateQueue.UpdateProgress(queueItem.QueueId, progress);

            DownloadProgress?.Invoke(this, new ExtensionDownloadProgressEventArgs
            {
                ExtensionId = queueItem.Extension.Metadata.Id,
                ExtensionName = queueItem.Extension.Metadata.Name,
                Progress = progress,
                TotalBytes = args.TotalBytesToReceive,
                ReceivedBytes = args.BytesReceived,
                Speed = args.Speed,
                RemainingTime = args.Remaining
            });
        }

        private void OnMultiDownloadCompleted(ExtensionUpdateQueueItem queueItem, MultiDownloadCompletedEventArgs args)
        {
            if (!args.IsComplated)
            {
                _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, "Download completed with errors");
            }
        }

        private void OnMultiDownloadError(ExtensionUpdateQueueItem queueItem, MultiDownloadErrorEventArgs args)
        {
            _updateQueue.UpdateStatus(queueItem.QueueId, ExtensionUpdateStatus.UpdateFailed, args.Exception?.Message);
        }

        private void OnDownloadCompleted(string extensionId, string extensionName)
        {
            DownloadCompleted?.Invoke(this, new ExtensionEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName
            });
        }

        private void OnDownloadFailed(string extensionId, string extensionName)
        {
            DownloadFailed?.Invoke(this, new ExtensionEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = extensionName
            });
        }
    }
}
