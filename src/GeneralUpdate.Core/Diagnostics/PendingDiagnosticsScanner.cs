using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Scans Bowl crash-evidence outboxes under <c>{install}/fail</c> and
///     <c>{stateRoot}/crash-outbox</c> (<c>{version}/{guid}/</c>) and
///     uploads pending evidence to the server. This is the arrival channel for crash
///     diagnostics — Bowl writes evidence to disk, Core delivers it.
/// </summary>
/// <remarks>
///     <para>
///         A per-root lock serializes client scans. The flow runs in the background at client
///         start. The disk contract uses the following rules:
///     </para>
///     <list type="number">
///         <item>existing means pending — a <c>{guid}/</c> directory present means "not uploaded yet";</item>
///         <item>uploaded means deleted — the directory is removed only after server acknowledgment;</item>
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
    private readonly string? _crashOutboxRoot;
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
        IDiagnosticsUploader uploader, IBlackMatcher? privacyFilter = null, string? stateRoot = null)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentNullException(nameof(installPath));
        _failRoot = Path.Combine(installPath, "fail");
        _crashOutboxRoot = string.IsNullOrWhiteSpace(stateRoot) ? null : Path.Combine(stateRoot, "crash-outbox");
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
            await ScanRootAsync(_failRoot, false, ct).ConfigureAwait(false);
            if (_crashOutboxRoot != null)
                await ScanRootAsync(_crashOutboxRoot, true, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"PendingDiagnosticsScanner: scan aborted: {ex.Message}");
        }
    }

    private async Task ScanRootAsync(string root, bool requireDump, CancellationToken ct)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            using var scanLock = new FileStream(Path.Combine(root, ".scan.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            foreach (var versionDir in Directory.EnumerateDirectories(root))
            {
                ct.ThrowIfCancellationRequested();
                if ((File.GetAttributes(versionDir) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var caseDir in Directory.EnumerateDirectories(versionDir))
                {
                    ct.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(caseDir) & FileAttributes.ReparsePoint) != 0) continue;
                    if (!Guid.TryParseExact(Path.GetFileName(caseDir), "D", out _)) continue;
                    await ProcessCaseAsync(caseDir, requireDump, ct).ConfigureAwait(false);
                }
            }
        }
        catch (IOException ex)
        {
            GeneralTracer.Warn($"PendingDiagnosticsScanner: could not scan {root}: {ex.Message}");
        }
    }

    private async Task ProcessCaseAsync(string caseDir, bool requireDump, CancellationToken ct)
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

            if (!Guid.TryParseExact(manifest.Guid, "D", out var manifestGuid) ||
                !Guid.TryParseExact(Path.GetFileName(caseDir), "D", out var directoryGuid) ||
                manifestGuid != directoryGuid ||
                !string.Equals(manifest.Kind, DiagnosticsKinds.Crash, StringComparison.OrdinalIgnoreCase))
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: skipping {caseDir} — invalid crash manifest identity.");
                return;
            }

            if (string.Equals(manifest.State, OutboxStates.Giveup, StringComparison.OrdinalIgnoreCase))
                return; // Retry budget exhausted; Bowl's disk GC owns the directory from here.

            if (requireDump && !IsSafeCrashCase(caseDir))
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: crash case is incomplete or contains links; retaining {caseDir}.");
                return;
            }

            var packed = DiagnosticsPackager.PackDirectory(caseDir, _options, _privacyFilter);
            if (packed == null)
            {
                RecordFailure(manifestPath, manifest);
                return;
            }
            if (requireDump && !packed.Value.Entries.Any(entry =>
                    string.Equals(Path.GetExtension(entry), ".dmp", StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(packed.Value.ZipPath);
                GeneralTracer.Warn($"PendingDiagnosticsScanner: crash dump was excluded or too large; retaining {caseDir}.");
                RecordFailure(manifestPath, manifest);
                return;
            }

            // The outbox manifest's guid travels to the server: it is the idempotency key, so a
            // retried upload of the same evidence directory deduplicates instead of duplicating.
            var bundle = new DiagnosticsBundle(packed.Value.ZipPath, new DiagnosticsManifest
            {
                Guid = manifest.Guid,
                Kind = manifest.Kind,
                TargetName = manifest.TargetName,
                Version = manifest.Version,
                AppKey = _options.AppKey,
                ProductId = _options.ProductId,
                Platform = _options.Platform,
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

    private static bool IsSafeCrashCase(string caseDir)
    {
        var directories = new Stack<string>();
        directories.Push(caseDir);
        var hasDump = false;
        while (directories.Count > 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directories.Pop()))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                if ((attributes & FileAttributes.Directory) != 0)
                    directories.Push(path);
                else if (string.Equals(Path.GetExtension(path), ".dmp", StringComparison.OrdinalIgnoreCase) &&
                         new FileInfo(path).Length > 0)
                    hasDump = true;
            }
        }
        return hasDump;
    }

    private void RecordFailure(string manifestPath, OutboxManifest manifest)
    {
        try
        {
            manifest.Attempts++;
            if (_options.MaxRetry > 0 && manifest.Attempts >= _options.MaxRetry)
            {
                manifest.State = OutboxStates.Giveup;
                GeneralTracer.Warn($"PendingDiagnosticsScanner: {manifest.Guid} marked giveup after {manifest.Attempts} attempts.");
            }

            var tempPath = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, JsonSerializer.Serialize(manifest, DiagnosticsJsonContext.Default.OutboxManifest));
                File.Replace(tempPath, manifestPath, null);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"PendingDiagnosticsScanner: could not persist retry state for {manifest.Guid}: {ex.Message}");
        }
    }
}
