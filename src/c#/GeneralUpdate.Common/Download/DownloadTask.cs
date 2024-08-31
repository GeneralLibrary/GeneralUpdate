using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;

namespace GeneralUpdate.Core.Download
{
    public class DownloadTask<TVersion>
    {
        #region Private Members

        private readonly HttpClient _httpClient;
        private readonly DownloadManager<TVersion> _manager;
        private readonly TVersion _version;
        private const int DEFAULT_DELTA = 1048576; // 1024*1024
        private long _beforBytes;
        private long _receivedBytes;
        private long _totalBytes;
        private Timer _speedTimer;
        private DateTime _startTime;

        #endregion Private Members

        #region Constructors

        public DownloadTask(DownloadManager<TVersion> manager, TVersion version)
        {
            _manager = manager;
            _version = version;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_manager.TimeOut)
            };
        }

        #endregion Constructors

        #region Public Properties

        public bool IsCompleted { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public async Task LaunchAsync()
        {
            try
            {
                var url = GetPropertyValue<string>(_version, "Url");
                var name = GetPropertyValue<string>(_version, "Name");
                var installPath = $"{_manager.Path}{name}{_manager.Format}";

                InitStatisticsEvent();
                InitProgressEvent();
                InitCompletedEvent();

                await DownloadFileRangeAsync(url, installPath);
            }
            catch (Exception ex)
            {
                _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(ex, _version));
                //throw new GeneralUpdateException<ExceptionArgs>("'download task' The executes abnormally !", ex);
            }
        }

        #endregion Public Methods

        #region Private Methods

        private async Task DownloadFileRangeAsync(string url, string path)
        {
            var tempPath = path + ".temp";
            long startPos = CheckFile(tempPath);

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to download file: {response.ReasonPhrase}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            if (startPos >= totalBytes)
            {
                File.Move(tempPath, path);
                OnCompleted();
                return;
            }

            await foreach (var chunk in DownloadChunksAsync(response))
            {
                await WriteFileAsync(tempPath, chunk, totalBytes);
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

        private long CheckFile(string tempPath)
        {
            long startPos = 0;
            if (File.Exists(tempPath))
            {
                using var fileStream = File.OpenWrite(tempPath);
                startPos = fileStream.Length;
                fileStream.Seek(startPos, SeekOrigin.Current);
            }
            return startPos;
        }

        private async Task WriteFileAsync(string tempPath, byte[] chunk, long totalBytes)
        {
            using var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None);
            await fileStream.WriteAsync(chunk, 0, chunk.Length);
            _receivedBytes += chunk.Length;
            OnProgressChanged(_receivedBytes, totalBytes);

            if (_receivedBytes >= totalBytes)
            {
                fileStream.Close();
                File.Move(tempPath, tempPath.Replace(".temp", ""));
                OnCompleted();
            }
        }

        private void InitStatisticsEvent()
        {
            if (_speedTimer != null) return;

            _speedTimer = new Timer(_ =>
            {
                try
                {
                    var interval = DateTime.Now - _startTime;
                    var downloadSpeed = interval.Seconds < 1
                        ? ToUnit(_receivedBytes - _beforBytes)
                        : ToUnit((_receivedBytes - _beforBytes) / interval.Seconds);
                    var size = (_totalBytes - _receivedBytes) / DEFAULT_DELTA;
                    var remainingTime = new DateTime().AddSeconds(Convert.ToDouble(size));
                    _manager.OnMultiDownloadStatistics(this, new MultiDownloadStatisticsEventArgs(_version, remainingTime, downloadSpeed));
                    _startTime = DateTime.Now;
                    _beforBytes = _receivedBytes;
                }
                catch (Exception exception)
                {
                    _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
                }
            }, null, 0, 1000);
        }

        private void InitProgressEvent()
        {
            _manager.MultiDownloadProgressChanged += (sender, e) =>
            {
                try
                {
                    _receivedBytes = e.BytesReceived;
                    _totalBytes = e.TotalBytesToReceive;

                    var eventArgs = new MultiDownloadProgressChangedEventArgs(_version,
                        e.BytesReceived / DEFAULT_DELTA,
                        e.TotalBytesToReceive / DEFAULT_DELTA,
                        e.ProgressPercentage,
                        e.UserState);

                    _manager.OnMultiDownloadProgressChanged(this, eventArgs);
                }
                catch (Exception exception)
                {
                    _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
                }
            };
        }

        private void InitCompletedEvent()
        {
            _manager.MultiDownloadCompleted += (sender, e) =>
            {
                try
                {
                    _speedTimer?.Dispose();
                    var eventArgs = new MultiDownloadCompletedEventArgs(_version, e.Error, e.Cancelled, e.UserState);
                    _manager.OnMultiAsyncCompleted(this, eventArgs);
                }
                catch (Exception exception)
                {
                    _manager.OnMultiDownloadError(this, new MultiDownloadErrorEventArgs(exception, _version));
                }
                finally
                {
                    IsCompleted = true;
                }
            };
        }

        private void OnProgressChanged(long bytesReceived, long totalBytes)
        {
            _manager.OnMultiDownloadProgressChanged(this, new MultiDownloadProgressChangedEventArgs(
                bytesReceived, totalBytes, (float)bytesReceived / totalBytes, _version));
        }

        private void OnCompleted()
        {
            _manager.OnMultiAsyncCompleted(this, new MultiDownloadCompletedEventArgs(_version, null, false, _version));
        }

        private TResult GetPropertyValue<TResult>(TVersion entity, string propertyName)
        {
            TResult result = default(TResult);
            try
            {
                var propertyInfo = typeof(TVersion).GetProperty(propertyName);
                result = (TResult)propertyInfo?.GetValue(entity);
            }
            catch (Exception ex)
            {
                //throw new GeneralUpdateException<ExceptionArgs>($"Error getting property value: {ex.Message}", ex);
            }
            return result;
        }

        private string ToUnit(long byteSize)
        {
            var tempSize = Convert.ToSingle(byteSize) / 1024;
            if (tempSize > 1)
            {
                var tempMbyte = tempSize / 1024;
                return tempMbyte > 1
                    ? $"{tempMbyte:##0.00}MB/S"
                    : $"{tempSize:##0.00}KB/S";
            }
            return $"{byteSize}B/S";
        }

        #endregion Private Methods
    }
}
