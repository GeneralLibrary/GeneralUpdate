using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Scans the Bowl crash-evidence outbox (<c>{install}/fail/{version}/{guid}/</c>) and
///     uploads pending evidence to the server. This is the arrival channel for crash
///     diagnostics — Bowl writes evidence to disk, Core delivers it.
/// </summary>
/// <remarks>
///     <para>
///         Pure disk-contract interaction: read-only scanning without locks, the whole flow
///         wrapped so it can run fire-and-forget in the background of a client start. The
///         three no-coordination rules (docs/bowl-redesign.md §3.2):
///     </para>
///     <list type="number">
///         <item>existing means pending — a <c>{guid}/</c> directory present means "not uploaded yet";</item>
///         <item>uploaded means deleted — the whole directory is removed after a 2xx response;</item>
///         <item>schema tolerance — manifests with a schema other than 2 are skipped with a warning.</item>
///     </list>
///     <para>
///         Failed uploads increment the attempt counter persisted in the manifest; once the
///         counter reaches <see cref="DiagnosticsOptions.MaxRetry"/> the state flips to
///         <see cref="OutboxStates.Giveup"/> and the scanner stops trying (the Bowl watchdog's
///         disk GC removes the directory eventually). Evidence is never deleted after a failure.
///     </para>
/// </remarks>
public sealed class PendingDiagnosticsScanner
{
    /// <summary>Root of the outbox: <c>{installPath}/fail</c>.</summary>
    private readonly string _failRoot;
    private readonly DiagnosticsOptions _options;
    private readonly IDiagnosticsUploader _uploader;
    private readonly IBlackMatcher? _privacyFilter;

    /// <param name="installPath">Application install root (contains the <c>fail/</c> outbox).</param>
    /// <param name="options">Runtime options (retry budget, size cap).</param>
    /// <param name="uploader">Configured uploader.</param>
    /// <param name="privacyFilter">
    ///     Optional blacklist filter applied when packaging evidence. Injected (not read from
    ///     the ambient static) so the scan behaves deterministically regardless of other state.
    /// </param>
    public PendingDiagnosticsScanner(string installPath, DiagnosticsOptions options,
        IDiagnosticsUploader uploader, IBlackMatcher? privacyFilter = null)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentNullException(nameof(installPath));
        _failRoot = Path.Combine(installPath, "fail");
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        _privacyFilter = privacyFilter;
    }

    /// <summary>
    ///     Runs one catch-up scan. Never throws for I/O problems — suitable for a
    ///     fire-and-forget background task started at <c>ClientStrategy.ExecuteAsync</c> entry.
    /// </summary>
    public Task ScanAsync(CancellationToken ct) => Task.Run(() => ScanCoreAsync(ct), ct);

    /// <summary>
    ///     Starts one scan on the thread pool and detaches. All exceptions are contained inside
    ///     <see cref="ScanCoreAsync"/>; this method cannot fault.
    /// </summary>
    public void StartBackgroundScan()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ScanCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: background scan aborted: {ex.Message}");
            }
        });
    }

    private async Task ScanCoreAsync(CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(_failRoot))
                return;

            foreach (var versionDir in Directory.EnumerateDirectories(_failRoot))
            {
                ct.ThrowIfCancellationRequested();
                foreach (var caseDir in Directory.EnumerateDirectories(versionDir))
                {
                    ct.ThrowIfCancellationRequested();
                    await ProcessCaseAsync(caseDir, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One broken directory must not take down the scan loop or the caller.
            GeneralTracer.Warn($"PendingDiagnosticsScanner: scan aborted: {ex.Message}");
        }
    }

    private async Task ProcessCaseAsync(string caseDir, CancellationToken ct)
    {
        try
        {
            var manifestPath = Path.Combine(caseDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: skipping {caseDir} — no manifest.json.");
                return;
            }

            var manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath), DiagnosticsJsonContext.Default.OutboxManifest);
            if (manifest == null || manifest.Schema != OutboxManifest.SupportedSchema)
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: skipping {caseDir} — unsupported manifest schema {manifest?.Schema.ToString() ?? "null"}.");
                return;
            }

            if (string.Equals(manifest.State, OutboxStates.Giveup, StringComparison.OrdinalIgnoreCase))
                return; // Retry budget exhausted; Bowl's disk GC owns the directory from here.

            var packed = DiagnosticsPackager.PackDirectory(caseDir, _options, _privacyFilter);
            if (packed == null)
            {
                RecordFailure(manifestPath, manifest);
                return;
            }

            var bundle = new DiagnosticsBundle(packed.Value.ZipPath, new DiagnosticsManifest
            {
                Kind = manifest.Kind,
                Version = manifest.Version,
                Reason = $"crash outbox {manifest.Guid}, exitCode={manifest.ExitCode?.ToString() ?? "n/a"}",
                Files = packed.Value.Entries,
            });

            if (await _uploader.UploadAsync(bundle, ct).ConfigureAwait(false))
            {
                StorageManager.DeleteDirectory(caseDir);
                GeneralTracer.Info($"PendingDiagnosticsScanner: uploaded and removed {caseDir}.");
                return;
            }

            RecordFailure(manifestPath, manifest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A single unreadable/deleted-under-us directory is reported, never fatal.
            GeneralTracer.Warn($"PendingDiagnosticsScanner: case {caseDir} failed: {ex.Message}");
        }
    }

    private void RecordFailure(string manifestPath, OutboxManifest manifest)
    {
        try
        {
            manifest.Attempts++;
            if (manifest.Attempts >= _options.MaxRetry)
            {
                manifest.State = OutboxStates.Giveup;
                GeneralTracer.Warn($"PendingDiagnosticsScanner: {manifest.Guid} marked giveup after {manifest.Attempts} attempts.");
            }

            File.WriteAllText(manifestPath,
                JsonSerializer.Serialize(manifest, DiagnosticsJsonContext.Default.OutboxManifest));
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"PendingDiagnosticsScanner: could not persist retry state for {manifest.Guid}: {ex.Message}");
        }
    }
}
