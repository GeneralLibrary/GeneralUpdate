using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.OTA
{
    /// <summary>
    /// Handles over-the-air firmware file download with progress reporting,
    /// automatic retry, and configurable timeout.
    /// 
    /// <para>
    /// The downloader reads the firmware binary in configurable chunks,
    /// reports progress via <see cref="FirmwareConfig.OnDownloadProgress"/>
    /// with speed and ETA, and retries transient failures according to
    /// the configuration.
    /// </para>
    /// </summary>
    public class FirmwareDownloader
    {
        /// <summary>
        /// Default buffer size for download chunks: 8192 bytes (8 KB).
        /// </summary>
        public const int DefaultBufferSize = 8192;

        private readonly FirmwareConfig _config;
        private readonly Action<long, long, double, TimeSpan> _onDownloadProgress;
        private readonly Action<FirmwareProgressInfo> _onProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirmwareDownloader"/> class.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="onDownloadProgress">
        /// Optional callback for download progress with speed and ETA.
        /// Parameters: (bytesReceived, totalBytes, speedBytesPerSecond, estimatedRemaining).
        /// </param>
        /// <param name="onProgress">
        /// Optional callback for rich progress information.
        /// </param>
        public FirmwareDownloader(
            FirmwareConfig config,
            Action<long, long, double, TimeSpan> onDownloadProgress = null,
            Action<FirmwareProgressInfo> onProgress = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _onDownloadProgress = onDownloadProgress;
            _onProgress = onProgress;
        }

        /// <summary>
        /// Downloads the firmware file from the configured URL to the specified local path.
        /// Supports automatic retry on transient failures and progress reporting.
        /// </summary>
        /// <param name="localFilePath">
        /// The full local path where the downloaded file will be saved.
        /// Parent directories are created automatically if they do not exist.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The full path to the downloaded file.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the firmware URL is not configured.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled or times out.
        /// </exception>
        /// <exception cref="WebException">
        /// Thrown when the download fails after all retry attempts.
        /// </exception>
        public async Task<string> DownloadAsync(string localFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_config.FirmwareUrl))
            {
                throw new InvalidOperationException(
                    "Firmware URL is not configured. Set FirmwareConfig.FirmwareUrl before downloading.");
            }

            FirmwareTrace.BeginOperation("FirmwareDownload");
            FirmwareTrace.Info("Download URL: {0}", _config.FirmwareUrl);
            FirmwareTrace.Info("Target path: {0}", localFilePath);

            // Ensure target directory exists
            string directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                FirmwareTrace.Debug("Created download directory: {0}", directory);
            }

            // Create combined timeout token
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds)))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                int attempt = 0;
                Exception lastException = null;
                var combinedToken = linkedCts.Token;

                while (attempt < _config.RetryCount)
                {
                    attempt++;
                    combinedToken.ThrowIfCancellationRequested();

                    try
                    {
                        FirmwareTrace.Info("Download attempt {0}/{1}", attempt, _config.RetryCount);

                        await DownloadWithProgressAsync(localFilePath, combinedToken)
                            .ConfigureAwait(false);

                        FirmwareTrace.EndOperation("FirmwareDownload", TimeSpan.Zero, true);
                        FirmwareTrace.Info("Download completed successfully: {0}", localFilePath);
                        return localFilePath;
                    }
                    catch (OperationCanceledException)
                    {
                        FirmwareTrace.Warn("Download cancelled on attempt {0}", attempt);
                        throw;
                    }
                    catch (WebException wex)
                    {
                        lastException = wex;
                        FirmwareTrace.Error(
                            "Download attempt {0}/{1} failed (WebException): {2}",
                            attempt,
                            _config.RetryCount,
                            wex.Message);

                        if (attempt < _config.RetryCount)
                        {
                            FirmwareTrace.Warn(
                                "Retrying in {0} seconds...",
                                _config.RetryDelaySeconds);
                            await Task.Delay(_config.RetryDelaySeconds * 1000, combinedToken)
                                .ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        FirmwareTrace.Error(
                            "Download attempt {0}/{1} failed: {2}",
                            attempt,
                            _config.RetryCount,
                            ex.Message);

                        if (attempt < _config.RetryCount)
                        {
                            FirmwareTrace.Warn(
                                "Retrying in {0} seconds...",
                                _config.RetryDelaySeconds);
                            await Task.Delay(_config.RetryDelaySeconds * 1000, combinedToken)
                                .ConfigureAwait(false);
                        }
                    }
                }

                // All retries exhausted
                FirmwareTrace.EndOperation("FirmwareDownload", TimeSpan.Zero, false);
                string errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Firmware download failed after {0} attempts. Last error: {1}",
                    _config.RetryCount,
                    lastException?.Message ?? "Unknown error");
                FirmwareTrace.Error(errorMessage);
                throw new WebException(errorMessage, lastException);
            }
        }

        /// <summary>
        /// Performs a single download attempt with progress reporting.
        /// Uses <see cref="HttpWebRequest"/> for broad .NET Standard 2.0 compatibility.
        /// Reports speed and ETA via <see cref="FirmwareConfig.OnDownloadProgress"/> and
        /// rich progress via <see cref="FirmwareConfig.OnProgress"/> at ~500 ms intervals.
        /// </summary>
        /// <param name="localFilePath">Where to save the downloaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task DownloadWithProgressAsync(string localFilePath, CancellationToken cancellationToken)
        {
            var request = WebRequest.CreateHttp(_config.FirmwareUrl);
            request.Method = "GET";

            // Configure timeout
            int timeoutMs = _config.TimeoutSeconds * 1000;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;

            using (cancellationToken.Register(() => request.Abort()))
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false) as HttpWebResponse)
                {
                    if (response == null)
                    {
                        throw new WebException("Failed to get HTTP response from firmware URL.");
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new WebException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Firmware download returned HTTP {0} ({1}).",
                                (int)response.StatusCode,
                                response.StatusDescription));
                    }

                    long contentLength = response.ContentLength;
                    long totalBytesRead = 0;

                    FirmwareTrace.Info("Download started. Content length: {0} bytes", contentLength);

                    using (var responseStream = response.GetResponseStream())
                    using (var fileStream = new FileStream(
                        localFilePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        DefaultBufferSize,
                        useAsync: true))
                    {
                        byte[] buffer = new byte[DefaultBufferSize];
                        int bytesRead;
                        var stageStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        long lastReportBytes = 0;
                        long lastReportTimeMs = 0;
                        var speedSamples = new Queue<double>(4);

                        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                            .ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                                .ConfigureAwait(false);

                            totalBytesRead += bytesRead;

                            // Report at ~500 ms intervals for smooth speed/ETA updates
                            long elapsedMs = stageStopwatch.ElapsedMilliseconds;
                            if (elapsedMs - lastReportTimeMs >= 500 ||
                                totalBytesRead == contentLength)
                            {
                                // --- Calculate instant speed and sliding-window average ---
                                double deltaBytes = totalBytesRead - lastReportBytes;
                                double deltaSeconds = (elapsedMs - lastReportTimeMs) / 1000.0;
                                double instantSpeed = deltaSeconds > 0 ? deltaBytes / deltaSeconds : 0;

                                speedSamples.Enqueue(instantSpeed);
                                if (speedSamples.Count > 3) speedSamples.Dequeue();

                                double avgSpeed = 0;
                                foreach (var s in speedSamples) avgSpeed += s;
                                avgSpeed = speedSamples.Count > 0 ? avgSpeed / speedSamples.Count : instantSpeed;

                                TimeSpan eta = contentLength > 0 && avgSpeed > 0
                                    ? TimeSpan.FromSeconds((contentLength - totalBytesRead) / avgSpeed)
                                    : TimeSpan.Zero;

                                // --- Stage progress ---
                                float stagePct = contentLength > 0
                                    ? (float)totalBytesRead / contentLength * 100f
                                    : 0f;

                                // --- Overall progress (download stage = 20%→70% band) ---
                                float overallPct = contentLength > 0
                                    ? 20f + (stagePct * 0.5f)
                                    : 20f;

                                // --- Trace ---
                                FirmwareTrace.Progress("Download", totalBytesRead, contentLength > 0 ? contentLength : totalBytesRead);

                                // --- New: rich download callback (bytes, total, speed, eta) ---
                                FireCallback(_onDownloadProgress, totalBytesRead, contentLength > 0 ? contentLength : totalBytesRead, avgSpeed, eta);
                                FireCallback(_config.OnDownloadProgress, totalBytesRead, contentLength > 0 ? contentLength : totalBytesRead, avgSpeed, eta);

                                // --- New: general progress callback ---
                                var info = new FirmwareProgressInfo
                                {
                                    Stage = FirmwareUpdateStage.Downloading,
                                    StageProgressPercent = stagePct,
                                    OverallProgressPercent = overallPct,
                                    BytesDownloaded = totalBytesRead,
                                    TotalBytes = contentLength,
                                    SpeedBytesPerSecond = avgSpeed,
                                    EstimatedRemaining = eta,
                                    Elapsed = stageStopwatch.Elapsed,
                                    StatusText = string.Format(
                                        CultureInfo.InvariantCulture,
                                        "下载中... {0}/{1} KB",
                                        totalBytesRead / 1024,
                                        contentLength > 0 ? contentLength / 1024 : -1)
                                };
                                FireCallback(_onProgress, info);
                                FireCallback(_config.OnProgress, info);

                                // --- Legacy: backward-compatible callback ---
#pragma warning disable 618
                                FireCallback(_config.ProgressCallback, totalBytesRead, contentLength > 0 ? contentLength : totalBytesRead);
#pragma warning restore 618

                                lastReportBytes = totalBytesRead;
                                lastReportTimeMs = elapsedMs;
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        stageStopwatch.Stop();

                        if (contentLength > 0 && totalBytesRead != contentLength)
                        {
                            throw new WebException(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Download incomplete: received {0} of {1} bytes.",
                                    totalBytesRead,
                                    contentLength));
                        }

                        double speedKBps = stageStopwatch.Elapsed.TotalSeconds > 0
                            ? (totalBytesRead / 1024.0) / stageStopwatch.Elapsed.TotalSeconds
                            : 0;

                        FirmwareTrace.Info(
                            "Download finished. Total: {0} bytes, Duration: {1:F1}s, Speed: {2:F1} KB/s",
                            totalBytesRead,
                            stageStopwatch.Elapsed.TotalSeconds,
                            speedKBps);
                    }
                }
            }
        }

        /// <summary>
        /// Safely invokes a 4-parameter callback, catching and logging any exceptions.
        /// Prevents a misbehaving callback from crashing the download pipeline.
        /// </summary>
        private static void FireCallback<T1, T2, T3, T4>(Action<T1, T2, T3, T4> callback, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (callback == null) return;
            try { callback(arg1, arg2, arg3, arg4); }
            catch (Exception ex) { FirmwareTrace.Warn("Download progress callback threw an exception (ignored): {0}", ex.Message); }
        }

        /// <summary>
        /// Safely invokes a 1-parameter callback, catching and logging any exceptions.
        /// </summary>
        private static void FireCallback<T>(Action<T> callback, T arg)
        {
            if (callback == null) return;
            try { callback(arg); }
            catch (Exception ex) { FirmwareTrace.Warn("Progress callback threw an exception (ignored): {0}", ex.Message); }
        }

        /// <summary>
        /// Safely invokes a 2-parameter callback, catching and logging any exceptions.
        /// </summary>
        private static void FireCallback<T1, T2>(Action<T1, T2> callback, T1 arg1, T2 arg2)
        {
            if (callback == null) return;
            try { callback(arg1, arg2); }
            catch (Exception ex) { FirmwareTrace.Warn("Progress legacy callback threw an exception (ignored): {0}", ex.Message); }
        }
    }
}
