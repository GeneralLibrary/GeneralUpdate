using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Download
{
    /// <summary>
    /// Provides a simplified interface for downloading files, supporting both single and parallel downloads.
    /// This class wraps the existing DownloadManager and DownloadTask for ease of use.
    /// </summary>
    public class DownloadClient
    {
        private readonly string _destinationPath;
        private readonly string _format;
        private readonly int _timeoutSeconds;

        /// <summary>
        /// Initializes a new instance of the DownloadClient class.
        /// </summary>
        /// <param name="destinationPath">The local path where files will be downloaded.</param>
        /// <param name="format">The file format/extension (e.g., ".zip", ".exe").</param>
        /// <param name="timeoutSeconds">Timeout in seconds for download operations. Default is 60 seconds.</param>
        public DownloadClient(string destinationPath, string format = ".zip", int timeoutSeconds = 60)
        {
            _destinationPath = destinationPath ?? throw new ArgumentNullException(nameof(destinationPath));
            _format = format ?? throw new ArgumentNullException(nameof(format));
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Downloads a single file asynchronously.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="fileName">The name of the file (without extension).</param>
        /// <returns>A DownloadResult indicating success or failure.</returns>
        public async Task<DownloadResult> DownloadAsync(string url, string fileName)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            var results = await DownloadAsync(new[] { new DownloadRequest(url, fileName) });
            return results.First();
        }

        /// <summary>
        /// Downloads multiple files in parallel.
        /// </summary>
        /// <param name="requests">Collection of download requests.</param>
        /// <returns>A list of DownloadResult for each file.</returns>
        public async Task<IList<DownloadResult>> DownloadAsync(IEnumerable<DownloadRequest> requests)
        {
            return await ExecuteDownloadAsync(requests, null);
        }

        /// <summary>
        /// Downloads multiple files in parallel with progress tracking.
        /// </summary>
        /// <param name="requests">Collection of download requests.</param>
        /// <param name="progressCallback">Callback to receive progress updates.</param>
        /// <returns>A list of DownloadResult for each file.</returns>
        public async Task<IList<DownloadResult>> DownloadWithProgressAsync(
            IEnumerable<DownloadRequest> requests,
            Action<DownloadProgress> progressCallback)
        {
            if (progressCallback == null)
                throw new ArgumentNullException(nameof(progressCallback));

            return await ExecuteDownloadAsync(requests, progressCallback);
        }

        private async Task<IList<DownloadResult>> ExecuteDownloadAsync(
            IEnumerable<DownloadRequest> requests,
            Action<DownloadProgress> progressCallback)
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));

            var requestList = requests.ToList();
            if (!requestList.Any())
                throw new ArgumentException("At least one download request must be provided.", nameof(requests));

            var manager = new DownloadManager(_destinationPath, _format, _timeoutSeconds);
            var results = new Dictionary<string, DownloadResult>();
            var completionSource = new TaskCompletionSource<bool>();

            // Subscribe to events
            SubscribeToDownloadEvents(manager, results, completionSource, progressCallback);

            // Add all download tasks
            foreach (var request in requestList)
            {
                var version = new VersionInfo
                {
                    Name = request.FileName,
                    Url = request.Url,
                    Format = _format
                };
                manager.Add(new DownloadTask(manager, version));
            }

            // Execute downloads
            try
            {
                await manager.LaunchTasksAsync();
                await completionSource.Task;
            }
            catch (Exception ex)
            {
                HandleDownloadException(requestList, results, ex);
            }

            // Ensure all requests have results
            EnsureAllResultsPresent(requestList, results);

            return requestList.Select(r => results[r.FileName]).ToList();
        }

        private void SubscribeToDownloadEvents(
            DownloadManager manager,
            Dictionary<string, DownloadResult> results,
            TaskCompletionSource<bool> completionSource,
            Action<DownloadProgress> progressCallback)
        {
            // Subscribe to progress events if callback provided
            if (progressCallback != null)
            {
                manager.MultiDownloadStatistics += (sender, e) =>
                {
                    if (e.Version is VersionInfo versionInfo)
                    {
                        progressCallback(new DownloadProgress
                        {
                            FileName = versionInfo.Name,
                            ProgressPercentage = e.ProgressPercentage,
                            Speed = e.Speed,
                            RemainingTime = e.Remaining,
                            TotalBytes = e.TotalBytesToReceive,
                            ReceivedBytes = e.BytesReceived
                        });
                    }
                };
            }

            // Subscribe to completion events
            manager.MultiDownloadCompleted += (sender, e) =>
            {
                if (e.Version is VersionInfo versionInfo && !string.IsNullOrEmpty(versionInfo.Name))
                {
                    results[versionInfo.Name] = new DownloadResult
                    {
                        FileName = versionInfo.Name,
                        Success = e.IsComplated,
                        Url = versionInfo.Url,
                        Error = e.IsComplated ? null : "Download failed"
                    };
                }
            };

            manager.MultiDownloadError += (sender, e) =>
            {
                if (e.Version is VersionInfo versionInfo && !string.IsNullOrEmpty(versionInfo.Name))
                {
                    results[versionInfo.Name] = CreateFailedResult(versionInfo.Name, versionInfo.Url, e.Exception?.Message);
                }
            };

            manager.MultiAllDownloadCompleted += (sender, e) =>
            {
                completionSource.TrySetResult(e.IsAllDownloadCompleted);
            };
        }

        private void HandleDownloadException(
            List<DownloadRequest> requestList,
            Dictionary<string, DownloadResult> results,
            Exception ex)
        {
            foreach (var request in requestList)
            {
                if (!results.ContainsKey(request.FileName))
                {
                    results[request.FileName] = CreateFailedResult(request.FileName, request.Url, ex.Message);
                }
            }
        }

        private void EnsureAllResultsPresent(
            List<DownloadRequest> requestList,
            Dictionary<string, DownloadResult> results)
        {
            foreach (var request in requestList)
            {
                if (!results.ContainsKey(request.FileName))
                {
                    results[request.FileName] = CreateFailedResult(request.FileName, request.Url, "Download did not complete");
                }
            }
        }

        private DownloadResult CreateFailedResult(string fileName, string url, string error)
        {
            return new DownloadResult
            {
                FileName = fileName,
                Success = false,
                Url = url,
                Error = error ?? "Unknown error"
            };
        }
    }

    /// <summary>
    /// Represents a download request.
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>
        /// Gets or sets the URL of the file to download.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the file name (without extension).
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Initializes a new instance of the DownloadRequest class.
        /// </summary>
        public DownloadRequest() { }

        /// <summary>
        /// Initializes a new instance of the DownloadRequest class.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="fileName">The file name (without extension).</param>
        public DownloadRequest(string url, string fileName)
        {
            Url = url;
            FileName = fileName;
        }
    }

    /// <summary>
    /// Represents the result of a download operation.
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// Gets or sets the file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the URL that was downloaded.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets whether the download was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the download failed.
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Represents download progress information.
    /// </summary>
    public class DownloadProgress
    {
        /// <summary>
        /// Gets or sets the file name being downloaded.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the download progress as a percentage (0-100).
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the current download speed (formatted string, e.g., "1.5 MB/s").
        /// </summary>
        public string Speed { get; set; }

        /// <summary>
        /// Gets or sets the estimated remaining time.
        /// </summary>
        public TimeSpan RemainingTime { get; set; }

        /// <summary>
        /// Gets or sets the total size in bytes.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes received so far.
        /// </summary>
        public long ReceivedBytes { get; set; }
    }
}
