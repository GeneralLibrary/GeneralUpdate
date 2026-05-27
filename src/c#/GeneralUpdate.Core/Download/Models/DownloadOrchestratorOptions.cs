using System;

namespace GeneralUpdate.Core.Download.Models;

/// <summary>
/// Bundles all configurable download behaviour options into a single value object.
/// Used by <see cref="Orchestrators.DefaultDownloadOrchestrator"/> and
/// <see cref="Executors.HttpDownloadExecutor"/> to avoid constructor parameter explosion.
/// </summary>
public class DownloadOrchestratorOptions
{
    /// <summary>
    /// Maximum number of concurrent download operations.
    /// Valid range: 1 to <see cref="Environment.ProcessorCount"/> * 2.
    /// Default: 2.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Whether to resume interrupted downloads via HTTP Range requests.
    /// Default: true.
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed download operations.
    /// Default: 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Initial retry interval for exponential back-off.
    /// Actual delay before N-th retry = <c>RetryInterval * 2^(N-1)</c>.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to perform SHA256 checksum verification after download.
    /// Default: true.
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    /// Diff/patch generation mode — Serial or Parallel.
    /// When <see cref="Configuration.DiffMode.Serial"/>, <see cref="MaxConcurrency"/> is forced to 1.
    /// Default: <see cref="Configuration.DiffMode.Serial"/>.
    /// </summary>
    public Configuration.DiffMode DiffMode { get; set; } = Configuration.DiffMode.Serial;

    /// <summary>
    /// HTTP download timeout duration.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// File format for downloaded packages.
    /// Ensures the downloaded filename matches what the pipeline expects.
    /// Default: <see cref="Configuration.Format.Zip"/>.
    /// </summary>
    public Configuration.Format Format { get; set; } = Configuration.Format.Zip;

    /// <summary>
    /// Creates a <see cref="DownloadOrchestratorOptions"/> from <see cref="Configuration.GlobalConfigInfo"/>.
    /// </summary>
    public static DownloadOrchestratorOptions From(Configuration.GlobalConfigInfo config)
    {
        return new DownloadOrchestratorOptions
        {
            MaxConcurrency = SanitizeMaxConcurrency(config.MaxConcurrency),
            EnableResume = config.EnableResume,
            RetryCount = Math.Max(0, config.RetryCount),
            RetryInterval = config.RetryInterval,
            VerifyChecksum = config.VerifyChecksum,
            DiffMode = config.DiffMode,
            DownloadTimeout = TimeSpan.FromSeconds(config.DownloadTimeOut > 0 ? config.DownloadTimeOut : 30),
            Format = config.Format,
        };
    }

    /// <summary>Clamps <paramref name="value"/> to [1, ProcessorCount * 2].</summary>
    public static int SanitizeMaxConcurrency(int value)
    {
        var max = Math.Max(1, Environment.ProcessorCount * 2);
        if (value < 1) return 1;
        if (value > max) return max;
        return value;
    }
}
