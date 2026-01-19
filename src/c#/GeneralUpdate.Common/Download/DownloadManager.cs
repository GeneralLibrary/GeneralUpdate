using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Download
{
    public class DownloadManager(string path, string format, int timeOut)
    {
        #region Private Members

        private readonly ImmutableList<DownloadTask>.Builder _downloadTasksBuilder = ImmutableList.Create<DownloadTask>().ToBuilder();
        private ImmutableList<DownloadTask> _downloadTasks;

        #endregion Private Members

        #region Public Properties

        public List<(object, string)> FailedVersions { get; } = new();

        public string Path => path;

        public string Format => format;

        public int TimeOut => timeOut;

        private ImmutableList<DownloadTask> DownloadTasks => _downloadTasks ?? _downloadTasksBuilder.ToImmutable();

        public event EventHandler<MultiAllDownloadCompletedEventArgs> MultiAllDownloadCompleted;
        public event EventHandler<MultiDownloadCompletedEventArgs> MultiDownloadCompleted;
        public event EventHandler<MultiDownloadErrorEventArgs> MultiDownloadError;
        public event EventHandler<MultiDownloadStatisticsEventArgs> MultiDownloadStatistics;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Launches all added download tasks in parallel using Task.WhenAll.
        /// This method supports concurrent downloading of multiple files.
        /// </summary>
        /// <returns>A task that completes when all downloads are finished.</returns>
        /// <exception cref="Exception">Thrown when an error occurs during the download process.</exception>
        public async Task LaunchTasksAsync()
        {
            try
            {
                var downloadTasks = DownloadTasks.Select(task => task.LaunchAsync()).ToList();
                await Task.WhenAll(downloadTasks);
                MultiAllDownloadCompleted?.Invoke(this, new MultiAllDownloadCompletedEventArgs(true, FailedVersions));
            }
            catch (Exception ex)
            {
                MultiAllDownloadCompleted?.Invoke(this, new MultiAllDownloadCompletedEventArgs(false, FailedVersions));
                throw new Exception($"Download manager error: {ex.Message}", ex);
            }
        }

        public void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        => MultiDownloadStatistics?.Invoke(this, e);

        public void OnMultiAsyncCompleted(object sender, MultiDownloadCompletedEventArgs e)
        => MultiDownloadCompleted?.Invoke(this, e);

        public void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            MultiDownloadError?.Invoke(this, e);
            FailedVersions.Add((e.Version, e.Exception.Message));
        }

        /// <summary>
        /// Adds a download task to the manager's task queue.
        /// Multiple tasks can be added to enable parallel downloading.
        /// </summary>
        /// <param name="task">The download task to add.</param>
        public void Add(DownloadTask task)
        {
            Debug.Assert(task != null);
            if (!_downloadTasksBuilder.Contains(task))
            {
                _downloadTasksBuilder.Add(task);
            }
        }

        #endregion Public Methods

        #region Simplified API for One-Time Downloads

        /// <summary>
        /// Downloads a single file asynchronously without the need to manage events or tasks manually.
        /// This is a simplified interface for one-time download operations.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="destinationPath">The local path where the file should be saved.</param>
        /// <param name="fileName">The name of the file (without extension).</param>
        /// <param name="format">The file format/extension (e.g., ".zip").</param>
        /// <param name="timeOut">Timeout in seconds for the download operation. Default is 60 seconds.</param>
        /// <returns>A task that completes when the download is finished. Returns true if successful, false otherwise.</returns>
        public static async Task<bool> DownloadFileAsync(string url, string destinationPath, string fileName, string format, int timeOut = 60)
        {
            var manager = new DownloadManager(destinationPath, format, timeOut);
            var version = new VersionInfo
            {
                Name = fileName,
                Url = url,
                Format = format
            };
            
            var taskCompleted = false;
            var taskSucceeded = false;
            var taskCompletionSource = new TaskCompletionSource<bool>();

            manager.MultiDownloadCompleted += (sender, e) =>
            {
                if (!taskCompleted)
                {
                    taskCompleted = true;
                    taskSucceeded = e.IsComplated;
                    taskCompletionSource.TrySetResult(e.IsComplated);
                }
            };

            manager.MultiDownloadError += (sender, e) =>
            {
                if (!taskCompleted)
                {
                    taskCompleted = true;
                    taskCompletionSource.TrySetResult(false);
                }
            };

            manager.Add(new DownloadTask(manager, version));
            
            try
            {
                await manager.LaunchTasksAsync();
                await taskCompletionSource.Task;
                return taskSucceeded;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Downloads multiple files in parallel asynchronously without the need to manage events or tasks manually.
        /// This is a simplified interface for batch download operations.
        /// </summary>
        /// <param name="files">A collection of tuples containing (url, fileName) pairs to download.</param>
        /// <param name="destinationPath">The local path where the files should be saved.</param>
        /// <param name="format">The file format/extension (e.g., ".zip").</param>
        /// <param name="timeOut">Timeout in seconds for each download operation. Default is 60 seconds.</param>
        /// <returns>A task that completes when all downloads are finished. Returns a dictionary mapping file names to their download success status.</returns>
        public static async Task<Dictionary<string, bool>> DownloadFilesAsync(IEnumerable<(string url, string fileName)> files, string destinationPath, string format, int timeOut = 60)
        {
            var manager = new DownloadManager(destinationPath, format, timeOut);
            var results = new Dictionary<string, bool>();
            var taskCompletionSource = new TaskCompletionSource<bool>();

            manager.MultiDownloadCompleted += (sender, e) =>
            {
                if (e.Version is VersionInfo versionInfo && versionInfo.Name != null)
                {
                    results[versionInfo.Name] = e.IsComplated;
                }
            };

            manager.MultiAllDownloadCompleted += (sender, e) =>
            {
                taskCompletionSource.TrySetResult(e.IsAllDownloadCompleted);
            };

            foreach (var (url, fileName) in files)
            {
                var version = new VersionInfo
                {
                    Name = fileName,
                    Url = url,
                    Format = format
                };
                manager.Add(new DownloadTask(manager, version));
            }

            try
            {
                await manager.LaunchTasksAsync();
                await taskCompletionSource.Task;
            }
            catch
            {
                // Errors are already tracked in results via events
            }

            return results;
        }

        #endregion Simplified API for One-Time Downloads
    }
}