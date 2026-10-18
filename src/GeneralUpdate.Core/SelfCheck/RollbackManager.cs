using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.FileSystem;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;
using UpdateReport = GeneralUpdate.Core.Download.Reporting.UpdateReport;
using UpdateStatus = GeneralUpdate.Core.Download.Reporting.UpdateStatus;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>Parameters of one rollback execution.</summary>
/// <param name="FailedVersion">The version that was judged unusable; recorded as the UpgradeFail marker.</param>
/// <param name="PreviousVersion">Rollback target version; when <c>null</c> only files are restored (manifest untouched).</param>
/// <param name="BackupDirectory">Backup directory recorded for this update; falls back to the latest backup when absent.</param>
/// <param name="RecordId">Update record id used for the Failure report (0 = report without a record).</param>
/// <param name="ReportType">Report type carried through to the server (1 = poll, 2 = push).</param>
/// <param name="Reason">Why the rollback happens (first self-check failure); diagnostics + logs only.</param>
public sealed record RollbackRequest(
    string FailedVersion,
    string? PreviousVersion,
    string? BackupDirectory,
    int RecordId,
    int ReportType,
    string Reason);

/// <summary>Outcome of one rollback execution; every step is reported independently.</summary>
/// <param name="FilesRestored">Whether the backup files were restored into the install directory.</param>
/// <param name="ManifestReverted">Whether the manifest ClientVersion was written back to the previous version.</param>
/// <param name="ActiveVersion">
///     The version actually in effect after the rollback, or <c>null</c> when the install
///     directory is in an uncertain state. Callers MUST NOT relaunch the application unless
///     this value is present — running on a partially restored tree is worse than staying down.
/// </param>
public sealed record RollbackResult(bool FilesRestored, bool ManifestReverted, string? ActiveVersion);

/// <summary>
///     Single entry point for "go back to the last known-good version". Shared by the updater's
///     self-check failure path and the client's orphan-marker fallback so both callers execute
///     exactly the same recovery sequence.
/// </summary>
/// <remarks>
///     <para>
///         Execution order (each step wrapped independently — one failing step never blocks the
///         next, everything is traced):
///     </para>
///     <list type="number">
///         <item>resolve the backup directory (request first, latest backup as fallback)</item>
///         <item>restore files (<see cref="StorageManager.Restore"/>)</item>
///         <item>revert the manifest ClientVersion (skipped without a previous version)</item>
///         <item>write the <c>UpgradeFail</c> marker — closes the long-standing gap where
///             <c>ClientStrategy.CheckFail()</c> had a reader but no writer, re-arming the
///             "do not reinstall this broken version" guard</item>
///         <item>report Failure to the server</item>
///         <item>(optional) collect + upload diagnostics in real time — NOT through the Bowl
///             outbox, because the updater cannot rely on a later client start</item>
///     </list>
///     <para>
///         The marker intercepts only "repeated failure of the same version"
///         (<c>CheckFail</c> compares <c>failVersion &gt;= targetVersion</c>); a fixed higher
///         version published later still installs normally.
///     </para>
/// </remarks>
public sealed class RollbackManager
{
    private readonly string _installPath;
    private readonly IUpdateReporter _reporter;
    private readonly IDiagnosticsUploader? _diagnosticsUploader;

    /// <param name="installPath">Installation root the rollback restores into.</param>
    /// <param name="reporter">Status reporter used for the Failure report.</param>
    /// <param name="diagnosticsUploader">
    ///     Optional diagnostics channel; when set, a rollback diagnostic bundle is collected and
    ///     uploaded in real time. Failures are logged, never propagated.
    /// </param>
    public RollbackManager(string installPath, IUpdateReporter reporter, IDiagnosticsUploader? diagnosticsUploader = null)
    {
        _installPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _diagnosticsUploader = diagnosticsUploader;
    }

    /// <summary>Executes the rollback sequence. Never throws for step failures; returns the aggregate outcome.</summary>
    public async Task<RollbackResult> RollbackAsync(RollbackRequest req, CancellationToken ct)
    {
        if (req == null) throw new ArgumentNullException(nameof(req));
        GeneralTracer.Warn(
            $"RollbackManager: rolling back {req.FailedVersion} -> {req.PreviousVersion ?? "(unknown)"}. Reason: {req.Reason}");

        var filesRestored = await RestoreFilesAsync(req, ct).ConfigureAwait(false);
        var manifestReverted = filesRestored && await RevertManifestAsync(req).ConfigureAwait(false);
        await MarkFailedVersionAsync(req).ConfigureAwait(false);
        await ReportFailureAsync(req).ConfigureAwait(false);
        await UploadDiagnosticsAsync(req, filesRestored, manifestReverted, ct).ConfigureAwait(false);

        // Only a restored AND manifest-consistent tree has a well-defined active version.
        var activeVersion = filesRestored && manifestReverted ? req.PreviousVersion : null;
        if (activeVersion == null)
            GeneralTracer.Error("RollbackManager: install directory left in an uncertain state — do NOT relaunch the application; operator inspection required.");

        return new RollbackResult(filesRestored, manifestReverted, activeVersion);
    }

    private async Task<bool> RestoreFilesAsync(RollbackRequest req, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() =>
            {
                var backupDir = req.BackupDirectory;
                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                {
                    GeneralTracer.Info($"RollbackManager: recorded backup not available ({backupDir}), falling back to the latest backup.");
                    backupDir = StorageManager.GetLatestBackup(_installPath);
                }

                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                {
                    // Degraded mode: without material nothing was restored — the caller must
                    // treat the install tree as unrestored (ActiveVersion stays null).
                    GeneralTracer.Warn("RollbackManager: no backup material — file rollback skipped (degraded mode).");
                    return false;
                }

                GeneralTracer.Info($"RollbackManager: restoring {backupDir} -> {_installPath}.");
                StorageManager.Restore(backupDir, _installPath);
                GeneralTracer.Info("RollbackManager: restore completed.");
                return true;
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("RollbackManager: file restore failed.", ex);
            return false;
        }
    }

    private async Task<bool> RevertManifestAsync(RollbackRequest req)
    {
        // Without a previous version there is nothing to revert to — restoring the files alone
        // already brings the old manifest back (it is part of the backup snapshot).
        if (string.IsNullOrWhiteSpace(req.PreviousVersion))
        {
            GeneralTracer.Warn("RollbackManager: previous version unknown — manifest revert skipped.");
            return false;
        }

        try
        {
            var manifestPath = Path.Combine(_installPath, ManifestInfo.FileName);
            if (!File.Exists(manifestPath))
            {
                // Nothing to revert AND nothing recording versions — report honestly.
                GeneralTracer.Warn($"RollbackManager: no manifest at {manifestPath} — manifest revert skipped.");
                return false;
            }

            await Task.Run(() => ManifestInfo.TryUpdateVersion(_installPath, clientVersion: req.PreviousVersion))
                .ConfigureAwait(false);
            GeneralTracer.Info($"RollbackManager: manifest ClientVersion reverted to {req.PreviousVersion}.");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("RollbackManager: manifest revert failed.", ex);
            return false;
        }
    }

    private async Task MarkFailedVersionAsync(RollbackRequest req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.FailedVersion)) return;
            await Task.Run(() => Environments.SetEnvironmentVariable("UpgradeFail", req.FailedVersion))
                .ConfigureAwait(false);
            GeneralTracer.Info($"RollbackManager: UpgradeFail marker set to {req.FailedVersion}.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("RollbackManager: failed to set UpgradeFail marker.", ex);
        }
    }

    private async Task ReportFailureAsync(RollbackRequest req)
    {
        try
        {
            await _reporter.ReportAsync(
                new UpdateReport(req.RecordId, (int)UpdateStatus.Failure, req.ReportType)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"RollbackManager: failure report failed: {ex.Message}");
        }
    }

    private async Task UploadDiagnosticsAsync(
        RollbackRequest req, bool filesRestored, bool manifestReverted, CancellationToken ct)
    {
        if (_diagnosticsUploader == null || !_diagnosticsUploader.IsConfigured)
            return;

        try
        {
            var collector = new DiagnosticsCollector();
            var result = new RollbackResult(filesRestored, manifestReverted,
                filesRestored && manifestReverted ? req.PreviousVersion : null);
            var resultJson = JsonSerializer.Serialize(result, JsonContext.DiagnosticsJsonContext.Default.RollbackResult);
            var bundle = await collector.CollectAsync(new DiagnosticsCollectRequest(
                _installPath,
                DiagnosticsKinds.Rollback,
                req.FailedVersion,
                req.Reason,
                SelfCheckReportJson: null,
                RollbackResultJson: resultJson), ct).ConfigureAwait(false);

            var uploaded = await _diagnosticsUploader.UploadAsync(bundle, ct).ConfigureAwait(false);
            GeneralTracer.Info($"RollbackManager: rollback diagnostics upload={(uploaded ? "succeeded" : "failed (kept local)")}");
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"RollbackManager: rollback diagnostics upload failed: {ex.Message}");
        }
    }
}
