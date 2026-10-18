using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.SelfCheck;

/// <summary>
///     RollbackManager tests: file restore, manifest revert, the UpgradeFail marker (write side
///     of the previously read-only guard — <c>ClientStrategy.CheckFail</c> consumes it), the
///     degraded no-backup path and ActiveVersion gating for the relaunch decision.
/// </summary>
public sealed class RollbackManagerTests : IDisposable
{
    private readonly string _installPath;
    private readonly RecordingReporter _reporter = new();

    public RollbackManagerTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), $"gu-rollback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
        try
        {
            // Clean the cross-test UpgradeFail marker this test may have written.
            var markerPath = Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc", "UpgradeFail.enc");
            if (File.Exists(markerPath)) File.Delete(markerPath);
        }
        catch { /* best effort */ }
    }

    /// <summary>Builds a realistic post-update scene: backup of the old tree, broken new tree.</summary>
    private string SeedBackupAndBrokenTree()
    {
        var appFile = Path.Combine(_installPath, "app.exe");
        File.WriteAllText(appFile, "OLD-VERSION-CONTENT");
        var manifest = new ManifestInfo { MainAppName = "app.exe", ClientVersion = "1.0.1" };
        manifest.Save(Path.Combine(_installPath, ManifestInfo.FileName));

        var backupDir = Path.Combine(_installPath, ".backups", "backup-test");
        StorageManager.Backup(_installPath, backupDir, new List<string>());

        // Simulate the failed update: manifest advanced, binary replaced with a broken payload.
        File.WriteAllText(appFile, "BROKEN-NEW-VERSION");
        manifest.ClientVersion = "1.0.2";
        manifest.Save(Path.Combine(_installPath, ManifestInfo.FileName));

        return backupDir;
    }

    [Fact]
    public async Task RollbackAsync_RestoresFilesAndManifest_AndSetsUpgradeFail()
    {
        var backupDir = SeedBackupAndBrokenTree();
        var manager = new RollbackManager(_installPath, _reporter);

        var result = await manager.RollbackAsync(new RollbackRequest(
            FailedVersion: "1.0.2", PreviousVersion: "1.0.1", BackupDirectory: backupDir,
            RecordId: 42, ReportType: 1, Reason: "test"), CancellationToken.None);

        Assert.True(result.FilesRestored);
        Assert.True(result.ManifestReverted);
        Assert.Equal("1.0.1", result.ActiveVersion);
        Assert.Equal("OLD-VERSION-CONTENT", File.ReadAllText(Path.Combine(_installPath, "app.exe")));

        var manifest = ManifestInfo.Load(Path.Combine(_installPath, ManifestInfo.FileName));
        Assert.Equal("1.0.1", manifest!.ClientVersion);

        // The marker must be readable by the existing CheckFail consumer.
        Assert.Equal("1.0.2", Environments.GetEnvironmentVariable("UpgradeFail"));

        var report = Assert.Single(_reporter.Reports);
        Assert.Equal((int)UpdateStatus.Failure, report.Status);
        Assert.Equal(42, report.RecordId);
    }

    [Fact]
    public async Task RollbackAsync_RecordedBackupMissing_FallsBackToLatestBackup()
    {
        SeedBackupAndBrokenTree();
        var latestBackup = GeneralUpdate.Core.FileSystem.StorageManager.GetLatestBackup(_installPath);
        Assert.NotNull(latestBackup);

        var manager = new RollbackManager(_installPath, _reporter);
        var result = await manager.RollbackAsync(new RollbackRequest(
            FailedVersion: "1.0.2", PreviousVersion: "1.0.1",
            BackupDirectory: @"Z:\definitely\gone",
            RecordId: 0, ReportType: 1, Reason: "test"), CancellationToken.None);

        Assert.True(result.FilesRestored);
        Assert.Equal("OLD-VERSION-CONTENT",
            File.ReadAllText(Path.Combine(_installPath, "app.exe")));
    }

    [Fact]
    public async Task RollbackAsync_NoBackupAtAll_DegradedModeCannotRelaunch()
    {
        // Empty install tree: nothing backed up, nothing to restore.
        var manager = new RollbackManager(_installPath, _reporter);

        var result = await manager.RollbackAsync(new RollbackRequest(
            FailedVersion: "1.0.2", PreviousVersion: "1.0.1", BackupDirectory: null,
            RecordId: 0, ReportType: 1, Reason: "test"), CancellationToken.None);

        Assert.False(result.FilesRestored);
        Assert.Null(result.ActiveVersion); // the caller must NOT relaunch on this
        // Reporting and the failure marker still happen — the degraded path is report-only.
        Assert.Equal((int)UpdateStatus.Failure, Assert.Single(_reporter.Reports).Status);
        Assert.Equal("1.0.2", Environments.GetEnvironmentVariable("UpgradeFail"));
    }

    [Fact]
    public async Task RollbackAsync_UnknownPreviousVersion_SkipsManifestRevert()
    {
        var backupDir = SeedBackupAndBrokenTree();
        var manager = new RollbackManager(_installPath, _reporter);

        var result = await manager.RollbackAsync(new RollbackRequest(
            FailedVersion: "1.0.2", PreviousVersion: null, BackupDirectory: backupDir,
            RecordId: 0, ReportType: 1, Reason: "test"), CancellationToken.None);

        Assert.True(result.FilesRestored);
        Assert.False(result.ManifestReverted);
        Assert.Null(result.ActiveVersion);
        // The restore brought the OLD manifest back (it is part of the snapshot).
        var manifest = ManifestInfo.Load(Path.Combine(_installPath, ManifestInfo.FileName));
        Assert.Equal("1.0.1", manifest!.ClientVersion);
    }

    private sealed class RecordingReporter : GeneralUpdate.Core.Download.Reporting.IUpdateReporter
    {
        public List<GeneralUpdate.Core.Download.Reporting.UpdateReport> Reports { get; } = new();

        public Task ReportAsync(GeneralUpdate.Core.Download.Reporting.UpdateReport report,
            CancellationToken token = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }
    }
}
