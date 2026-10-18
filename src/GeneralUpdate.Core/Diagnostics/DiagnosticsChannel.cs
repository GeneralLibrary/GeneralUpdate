using System;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     The client's diagnostics channel: one place that owns the outbox scanner and the
///     uploader with a single shared <see cref="DiagnosticsOptions"/> instance.
/// </summary>
/// <remarks>
///     The bootstrap creates the channel when <see cref="Option.DiagnosticsUploadEnabled"/> is
///     on and hands it to the strategies — ClientStrategy runs the background outbox scan at
///     start, RollbackManager (via <see cref="DiagnosticsChannel.Uploader"/>) uploads real-time
///     bundles after a failed verification. A disabled channel is never constructed, so
///     call sites can treat a <c>null</c> channel as "feature off" without touching options.
/// </remarks>
public sealed class DiagnosticsChannel
{
    /// <summary>Shared runtime options (endpoint, size cap, retry budget).</summary>
    public DiagnosticsOptions Options { get; }

    /// <summary>Uploader used for real-time bundles (rollback / self-check / manual).</summary>
    public DiagnosticsUploader Uploader { get; }

    /// <summary>Scanner delivering the Bowl crash-evidence outbox.</summary>
    public PendingDiagnosticsScanner Scanner { get; }

    /// <param name="installPath">Application install root (locates the <c>fail/</c> outbox).</param>
    /// <param name="options">Runtime options. Must be enabled and carry a URL — see <see cref="IsUsable"/>.</param>
    /// <param name="privacyFilter">Optional blacklist filter applied when packaging evidence.</param>
    public DiagnosticsChannel(string installPath, DiagnosticsOptions options, IBlackMatcher? privacyFilter = null)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentNullException(nameof(installPath));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Uploader = new DiagnosticsUploader(options);
        Scanner = new PendingDiagnosticsScanner(installPath, options, Uploader, privacyFilter);
    }

    /// <summary><c>true</c> when the channel has a usable endpoint.</summary>
    public bool IsUsable => Uploader.IsConfigured;

    /// <summary>Detached catch-up scan of the crash-evidence outbox (fire-and-forget, never throws).</summary>
    public void StartBackgroundScan() => Scanner.StartBackgroundScan();
}
