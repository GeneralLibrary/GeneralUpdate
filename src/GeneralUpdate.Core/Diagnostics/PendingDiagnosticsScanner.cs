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
    private readonly string? _updateOutboxRoot;
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
        IDiagnosticsUploader uploader, IBlackMatcher? privacyFilter = null, string? stateRoot = null,
        string? installationId = null)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentNullException(nameof(installPath));
        _failRoot = Path.Combine(installPath, "fail");
        _crashOutboxRoot = string.IsNullOrWhiteSpace(stateRoot) ? null : Path.Combine(stateRoot, "crash-outbox");
        if (!string.IsNullOrWhiteSpace(stateRoot) && Guid.TryParseExact(installationId, "D", out _))
            _updateOutboxRoot = Path.Combine(stateRoot, "update-outbox", installationId);
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
            if (_updateOutboxRoot != null)
                await ScanUpdateOutboxAsync(ct).ConfigureAwait(false);
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

    private async Task ScanUpdateOutboxAsync(CancellationToken ct)
    {
        var root = _updateOutboxRoot!;
        if (!Directory.Exists(root)) return;
        for (var directory = new DirectoryInfo(root); directory != null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Update diagnostics outbox cannot traverse symbolic links.");
        using var scanLock = new FileStream(Path.Combine(root, ".scan.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        foreach (var caseDir in Directory.EnumerateDirectories(root))
        {
            ct.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(caseDir), "D", out _) ||
                (File.GetAttributes(caseDir) & FileAttributes.ReparsePoint) != 0) continue;
            await ProcessCaseAsync(caseDir, false, ct, DiagnosticsKinds.Update).ConfigureAwait(false);
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

    private async Task ProcessCaseAsync(string caseDir, bool requireDump, CancellationToken ct,
        string expectedKind = DiagnosticsKinds.Crash)
    {
        try
        {
            var manifestPath = Path.Combine(caseDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: skipping {caseDir} — no manifest.json.");
                return;
            }
            if (expectedKind == DiagnosticsKinds.Update && !IsSafeUpdateCase(caseDir))
            {
                GeneralTracer.Warn($"PendingDiagnosticsScanner: invalid update diagnostic case; retaining {caseDir}.");
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
                !string.Equals(manifest.Kind, expectedKind, StringComparison.OrdinalIgnoreCase) ||
                (expectedKind == DiagnosticsKinds.Update && (manifest.RecordId is null or <= 0 ||
                    manifest.RecordType is not (1 or 2) || !string.IsNullOrEmpty(manifest.TargetName) ||
                    !string.IsNullOrEmpty(manifest.Version) || string.IsNullOrWhiteSpace(_options.AppKey) ||
                    !string.Equals(manifest.State, OutboxStates.Pending, StringComparison.Ordinal))))
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
            if (expectedKind == DiagnosticsKinds.Update &&
                !packed.Value.Entries.Contains("diagnostic.txt", StringComparer.Ordinal))
            {
                File.Delete(packed.Value.ZipPath);
                GeneralTracer.Warn($"PendingDiagnosticsScanner: update diagnostic was excluded; retaining {caseDir}.");
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
                RecordId = manifest.RecordId,
                RecordType = manifest.RecordType,
                Reason = expectedKind == DiagnosticsKinds.Update ? "Update failed" :
                    $"crash outbox {manifest.Guid}, exitCode={manifest.ExitCode?.ToString() ?? "n/a"}",
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
            if (manifest.Kind != DiagnosticsKinds.Update && _options.MaxRetry > 0 && manifest.Attempts >= _options.MaxRetry)
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

    private static bool IsSafeUpdateCase(string caseDir)
    {
        var files = Directory.GetFileSystemEntries(caseDir);
        if (files.Length != 2) return false;
        var manifest = Path.Combine(caseDir, "manifest.json");
        var diagnostic = Path.Combine(caseDir, "diagnostic.txt");
        if (!File.Exists(manifest) || !File.Exists(diagnostic)) return false;
        foreach (var file in files)
            if ((File.GetAttributes(file) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
                new FileInfo(file).Length > 4096) return false;
        var text = File.ReadAllText(diagnostic);
        return new[] { "Download", "Verify", "Extract", "Apply", "Startup", "Rollback", "Unknown" }
            .Any(phase => text == "Update failure\nPhase: " + phase + "\n");
    }
}
