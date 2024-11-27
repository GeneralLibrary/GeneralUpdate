using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Download
{
    public class DownloadTask
    {
        #region Private Members

        private readonly HttpClient _httpClient;
        private readonly DownloadManager _manager;
        private readonly VersionInfo? _version;
        private Timer? _timer;
        private DateTime _startTime;
        private long _receivedBytes;
        private long _totalBytes;
        private long _currentBytes;

        #endregion Private Members

        public DownloadTask(DownloadManager manager, VersionInfo version)
        {
            _manager = manager;
            _version = version;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_manager.TimeOut) };
            _timer = new Timer(_=> Statistics(), null, 0, 1000);
        }

        public async Task LaunchAsync()
        {
            try
            {
                var path = Path.Combine(_manager.Path, $"{_version?.Name}{_manager.Format}");
                await DownloadFileRangeAsync(_version.Url, path);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
        }

        #region Private Methods

        private async Task DownloadFileRangeAsync(string url, string path)
        {
            try
            {
                var tempPath = path + ".temp";
                var startPos = CheckFile(tempPath);
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Failed to download file: {response.ReasonPhrase}");

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                Interlocked.Exchange(ref _totalBytes, totalBytes);

                if (startPos >= totalBytes)
                {
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }

                    File.Move(tempPath, path);
                    OnDownloadCompleted(true);
                    return;
                }

                await foreach (var chunk in DownloadChunksAsync(response))
                {
                    await WriteFileAsync(tempPath, chunk, totalBytes);
                }
            }
            catch (Exception exception)
            {
                OnDownloadCompleted(false);
                Debug.WriteLine(exception.Message);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
        }

        private async IAsyncEnumerable<byte[]> DownloadChunksAsync(HttpResponseMessage response)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var chunk = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                yield return chunk;
            }
        }
        
        private async Task WriteFileAsync(string tempPath, byte[] chunk, long totalBytes)
        {
            try
            {
                using var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None);
                await fileStream.WriteAsync(chunk, 0, chunk.Length);
                Interlocked.Add(ref _receivedBytes, chunk.Length);
                if (_receivedBytes >= totalBytes)
                {
                    fileStream.Close();
                    var path = tempPath.Replace(".temp", "");
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }

                    File.Move(tempPath, path);
                    OnDownloadCompleted(true);
                }
            }
            catch (Exception exception)
            {
                OnDownloadCompleted(false);
                Debug.WriteLine(exception);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
        }

        private void Statistics()
        {
            try
            {
                var interval = DateTime.Now - _startTime;
                var tempTotalBytes = Interlocked.Read(ref _totalBytes);
                //Accumulate the downloaded size.
                var tempReceivedBytes = Interlocked.Read(ref _receivedBytes);
                //Current downloaded size.
                var tempCurrentBytes = tempReceivedBytes - Interlocked.Read(ref _currentBytes);
                var speed = CalculateDownloadSpeed(tempCurrentBytes, interval);
                var formatSpeed = FormatDownloadSpeed(speed);
                var remainingTime = CalculateRemainingTime(tempTotalBytes, tempReceivedBytes, speed);
                var progress = CalculateDownloadProgress(tempTotalBytes, tempReceivedBytes);
                    
                var args = new MultiDownloadStatisticsEventArgs(_version
                    , remainingTime
                    , formatSpeed
                    , tempTotalBytes
                    , tempReceivedBytes
                    , progress);
                _manager.OnMultiDownloadStatistics(this, args);
                Interlocked.Exchange(ref _currentBytes, tempReceivedBytes);
                _startTime = DateTime.Now;
            }
            catch (Exception exception)
            {
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
        }

        private void OnDownloadCompleted(bool isComplated)
        {
            try
            {
                DisposeTimer();
                var eventArgs = new MultiDownloadCompletedEventArgs(_version, isComplated);
                _manager.OnMultiAsyncCompleted(this, eventArgs);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
        }

        /// <summary>
        /// Calculate the remaining download time (in seconds).
        /// </summary>
        /// <param name="totalBytes"></param>
        /// <param name="bytesReceived"></param>
        /// <param name="downloadSpeed"></param>
        /// <returns></returns>
        private static TimeSpan CalculateRemainingTime(long totalBytes, long bytesReceived, double downloadSpeed)
        {
            if (downloadSpeed == 0)
            {
                return new TimeSpan(0, 0, 0, 0);
            }

            var bytesRemaining = totalBytes - bytesReceived;
            var secondsRemaining = bytesRemaining / downloadSpeed;
            return TimeSpan.FromSeconds(secondsRemaining);
        }
        
        /// <summary>
        /// Calculate the download speed (in bytes per second).
        /// </summary>
        /// <param name="bytesReceived"></param>
        /// <param name="elapsedTime"></param>
        /// <returns></returns>
        private static double CalculateDownloadSpeed(long bytesReceived, TimeSpan elapsedTime)
        {
            if (elapsedTime.TotalSeconds == 0)
                return 0;
            
            return bytesReceived / elapsedTime.TotalSeconds;
        }
        
        /// <summary>
        /// Calculate the download progress (percentage %).
        /// </summary>
        /// <param name="totalBytes"></param>
        /// <param name="bytesReceived"></param>
        /// <returns></returns>
        private static double CalculateDownloadProgress(long totalBytes, long bytesReceived)
        {
            if (totalBytes == 0)
                return 0;
            
            return (double)bytesReceived / totalBytes * 100;
        }
        
        /// <summary>
        /// Convert the download speed to an appropriate unit (B, KB, MB, GB).
        /// </summary>
        /// <param name="speedInBytesPerSecond"></param>
        /// <returns></returns>
        private static string FormatDownloadSpeed(double speedInBytesPerSecond)
        {
            const double kiloByte = 1024;
            const double megaByte = kiloByte * 1024;
            const double gigaByte = megaByte * 1024;

            return speedInBytesPerSecond switch
            {
                >= gigaByte => $"{speedInBytesPerSecond / gigaByte:F2} GB/s",
                >= megaByte => $"{speedInBytesPerSecond / megaByte:F2} MB/s",
                _ => speedInBytesPerSecond >= kiloByte
                    ? $"{speedInBytesPerSecond / kiloByte:F2} KB/s"
                    : $"{speedInBytesPerSecond:F2} B/s"
            };
        }
        
        /// <summary>
        /// Get the size of the downloaded file for resuming interrupted downloads.
        /// </summary>
        /// <param name="tempPath"></param>
        /// <returns></returns>
        private static long CheckFile(string tempPath)
        {
            long startPos = 0;
            if (!File.Exists(tempPath)) return startPos;
            using var fileStream = File.OpenWrite(tempPath);
            startPos = fileStream.Length;
            fileStream.Seek(startPos, SeekOrigin.Current);
            return startPos;
        }

        private void DisposeTimer()
        {
            if (_timer == null) return;
            try
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                using var waitHandle = new ManualResetEvent(false);
                _timer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
            catch (ObjectDisposedException exception)
            {
                Debug.WriteLine("Timer has already been disposed: " + exception.Message);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
            catch (Exception exception)
            {
                Debug.WriteLine("An error occurred while disposing the timer: " + exception.Message);
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
            }
            finally
            {
                _timer = null;
            }
        }

        #endregion Private Methods
    }
}
