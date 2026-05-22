using System;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// A snapshot of the current progress in a firmware update operation.
    /// Passed to <see cref="Action{FirmwareProgressInfo}"/> callbacks
    /// to give callers rich information about the ongoing operation.
    /// </summary>
    public class FirmwareProgressInfo
    {
        /// <summary>Which stage the update is currently in.</summary>
        public FirmwareUpdateStage Stage { get; set; }

        /// <summary>
        /// Progress within the current stage, from 0.0 to 100.0.
        /// For example, during <see cref="FirmwareUpdateStage.Downloading"/> this
        /// reflects the percentage of bytes downloaded.
        /// </summary>
        public float StageProgressPercent { get; set; }

        /// <summary>
        /// Overall progress across all stages, from 0.0 to 100.0,
        /// weighted by the relative duration of each stage.
        /// Suitable for driving a single progress bar.
        /// </summary>
        public float OverallProgressPercent { get; set; }

        /// <summary>Bytes downloaded so far (only meaningful during <see cref="FirmwareUpdateStage.Downloading"/>).</summary>
        public long BytesDownloaded { get; set; }

        /// <summary>Total bytes to download. -1 if the server did not provide Content-Length.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Current download speed in bytes per second.</summary>
        public double SpeedBytesPerSecond { get; set; }

        /// <summary>Estimated time remaining for the current stage.</summary>
        public TimeSpan EstimatedRemaining { get; set; }

        /// <summary>Time elapsed since the current stage started.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>A human-readable status string (e.g., "下载中... 5120/10240 KB").</summary>
        public string StatusText { get; set; }

        /// <summary>Returns a compact string representation suitable for logging.</summary>
        public override string ToString()
        {
            return string.Format(
                "Stage={0}, Overall={1:F1}%, StagePct={2:F1}%, Bytes={3}/{4}, Speed={5:F1}KB/s, ETA={6}",
                Stage,
                OverallProgressPercent,
                StageProgressPercent,
                BytesDownloaded,
                TotalBytes,
                SpeedBytesPerSecond / 1024.0,
                EstimatedRemaining);
        }
    }
}
