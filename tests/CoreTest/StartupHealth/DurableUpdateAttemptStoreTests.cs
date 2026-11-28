using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.StartupHealth;
using GeneralUpdate.Core.Strategy;
using Moq;

namespace CoreTest.StartupHealth;

[Collection("NonParallel_EventManager")]
public sealed class DurableUpdateAttemptStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "durable-update-attempt-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Attempt_metadata_is_opt_in_and_round_trips_through_the_process_contract()
    {
        var legacyRequest = new UpdateRequest();
        Assert.Null(legacyRequest.DurableUpdateAttemptId);
        var legacyContract = new ProcessContract();
        Assert.Null(legacyContract.DurableUpdateAttemptId);
        Assert.Null(legacyContract.AttemptJournalDirectory);
        Assert.False(legacyContract.DisableLegacyRollback);

        Directory.CreateDirectory(_root);
        var attemptId = Guid.NewGuid();
        var source = new UpdateContext
        {
            MainAppName = "App.exe",
            InstallPath = _root,
            ClientVersion = "1.0.0",
            LastVersion = "2.0.0",
            AppSecretKey = "test-key",
            BackupDirectory = Path.Combine(_root, ".backups", "backup-test-" + attemptId.ToString("N")),
            TempPath = _root,
            Encoding = Encoding.UTF8,
            Format = Format.Zip,
            DurableUpdateAttemptId = attemptId
        };

        var contract = ConfigurationMapper.MapToProcessContract(
            source, new List<VersionEntry> { new() { Version = "2.0.0" } },
            new List<string>(), new List<string>(), new List<string>());
        var json = JsonSerializer.Serialize(contract, ProcessContractJsonContext.Default.ProcessContract);
        var reopened = JsonSerializer.Deserialize(json, ProcessContractJsonContext.Default.ProcessContract)!;

        Assert.Equal(attemptId, reopened.DurableUpdateAttemptId);
        Assert.Equal(DurableUpdateAttemptStore.GetDirectory(_root), reopened.AttemptJournalDirectory);
        Assert.True(reopened.DisableLegacyRollback);
        Assert.Equal(attemptId, ConfigurationMapper.MapToUpdateContext(new UpdateRequest
        {
            DurableUpdateAttemptId = attemptId
        }).DurableUpdateAttemptId);
    }

    [Fact]
    public void Legacy_backup_cleanup_does_not_create_attempt_journal_metadata()
    {
        var installPath = Path.Combine(_root, "install");
        Directory.CreateDirectory(Path.Combine(installPath, StorageManager.BackupRootDirectory));

        StorageManager.CleanBackup(installPath);

        Assert.False(Directory.Exists(DurableUpdateAttemptStore.GetDirectory(installPath)));
    }

    [Fact]
    public void Real_backup_is_bound_to_its_reserved_path_and_retention_keeps_the_pin()
    {
        var installPath = Path.Combine(_root, "install");
        var sourcePath = Path.Combine(_root, "source");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        File.WriteAllText(Path.Combine(sourcePath, "state.txt"), "snapshot");
        var attemptId = Guid.NewGuid();
        var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory,
            "backup-old-" + attemptId.ToString("N"));
        var store = new DurableUpdateAttemptStore(installPath);
        store.Begin(attemptId);
        store.ReserveSnapshot(attemptId, snapshotPath);
        StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true);
        store.BindSnapshot(attemptId, snapshotPath);

        for (var index = 0; index < 3; index++)
        {
            var other = Path.Combine(installPath, StorageManager.BackupRootDirectory, $"backup-new-{index}");
            Directory.CreateDirectory(other);
        }

        StorageManager.CleanBackup(installPath, keepVersions: 1);

        Assert.Equal(DurableUpdateAttemptStatus.SnapshotBound, store.GetAttempt(attemptId).Status);
        Assert.Equal(Path.GetFullPath(snapshotPath), Path.GetFullPath(store.GetAttempt(attemptId).SnapshotPath!));
        Assert.Equal("snapshot", File.ReadAllText(Path.Combine(snapshotPath, "state.txt")));
        Assert.DoesNotContain(DurableUpdateAttemptStore.GetDirectory(installPath), StorageManager.GetLatestBackup(installPath));
    }

    [Fact]
    public void Attempt_specific_backup_never_deletes_an_existing_destination()
    {
        var sourcePath = Path.Combine(_root, "source");
        var snapshotPath = Path.Combine(_root, ".backups", "backup-unique-attempt");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(snapshotPath);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "new");
        File.WriteAllText(Path.Combine(snapshotPath, "file.txt"), "existing");

        Assert.Throws<IOException>(() =>
            StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true));

        Assert.Equal("existing", File.ReadAllText(Path.Combine(snapshotPath, "file.txt")));
    }

    [Fact]
    public async Task Client_strategy_creates_and_binds_the_backup_used_by_its_update_workflow()
    {
        var installPath = Path.Combine(_root, "install");
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "app-state.txt"), "before-update");
        var attemptId = Guid.NewGuid();
        var asset = new DownloadAsset("client.zip", "https://example.test/client.zip", 10, null, "2.0.0",
            AppType: (int)AppType.Client);
        var source = new Mock<IDownloadSource>();
        source.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadSourceResult { Assets = new[] { asset }, HasMainUpdate = true });
        var executor = new Mock<IDownloadExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<DownloadAsset>(), It.IsAny<string>(),
                It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()))
            .Returns((DownloadAsset download, string path, IProgress<DownloadProgress>? _, CancellationToken _) =>
                Task.FromResult(new DownloadResult(download, path, 10, TimeSpan.Zero, 0, true, null)));
        var reports = new List<UpdateReport>();
        var reporter = new Mock<IUpdateReporter>();
        reporter.Setup(r => r.ReportAsync(It.IsAny<UpdateReport>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateReport, CancellationToken>((report, _) => reports.Add(report))
            .Returns(Task.CompletedTask);

        var strategy = new ClientStrategy
        {
            DownloadSource = source.Object,
            LaunchAfterPrepare = false,
            Reporter = reporter.Object,
            ProcessContractProvider = new EncryptedFileProcessContractProvider(Path.Combine(_root, "client-ipc"))
        };
        strategy.SetDownloadExecutor(executor.Object);
        strategy.Create(new UpdateContext
        {
            UpdateUrl = "https://example.test/update",
            ClientVersion = "1.0.0",
            AppSecretKey = "test-key",
            MainAppName = "App.exe",
            InstallPath = installPath,
            Encoding = Encoding.UTF8,
            DurableUpdateAttemptId = attemptId
        });

        try
        {
            await strategy.ExecuteAsync();
            Assert.True(strategy.HasPreparedClientUpdate);
            var attempt = new DurableUpdateAttemptStore(installPath).GetAttempt(attemptId);
            Assert.Equal(DurableUpdateAttemptStatus.SnapshotBound, attempt.Status);
            Assert.NotNull(attempt.SnapshotPath);
            Assert.Contains(attemptId.ToString("N"), Path.GetFileName(attempt.SnapshotPath));
            Assert.Equal("before-update", File.ReadAllText(Path.Combine(attempt.SnapshotPath, "app-state.txt")));
            Assert.DoesNotContain(reports, report => report.Status == (int)UpdateStatus.Success);
        }
        finally
        {
            EventManager.Instance.Clear();
        }
    }

    [Fact]
    public async Task Durable_client_update_rejects_bowl_before_backup_and_preserves_failure_reporting()
    {
        var installPath = Path.Combine(_root, "install-with-bowl");
        Directory.CreateDirectory(installPath);
        var reports = new List<UpdateReport>();
        var reporter = new Mock<IUpdateReporter>();
        reporter.Setup(r => r.ReportAsync(It.IsAny<UpdateReport>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateReport, CancellationToken>((report, _) => reports.Add(report))
            .Returns(Task.CompletedTask);

        var strategy = new ClientStrategy { Reporter = reporter.Object };
        strategy.Create(new UpdateContext
        {
            InstallPath = installPath,
            UpdateUrl = "https://example.test/update",
            AppSecretKey = "test-key",
            DurableUpdateAttemptId = Guid.NewGuid(),
            Bowl = "Bowl.exe"
        });

        try
        {
            await strategy.ExecuteAsync();
            Assert.False(Directory.Exists(DurableUpdateAttemptStore.GetDirectory(installPath)));
            Assert.DoesNotContain(Directory.Exists(Path.Combine(installPath, StorageManager.BackupRootDirectory))
                ? Directory.GetDirectories(Path.Combine(installPath, StorageManager.BackupRootDirectory))
                : Array.Empty<string>(), path => Path.GetFileName(path).StartsWith("backup-", StringComparison.Ordinal));
            Assert.Contains(reports, report => report.Status == (int)UpdateStatus.Failure);
            Assert.DoesNotContain(reports, report => report.Status == (int)UpdateStatus.Success);
        }
        finally
        {
            EventManager.Instance.Clear();
        }
    }

    [Fact]
    public async Task Durable_pipeline_suppresses_success_for_each_package_role_but_keeps_failure_reports()
    {
        var tempPath = Path.Combine(_root, "packages");
        Directory.CreateDirectory(tempPath);
        var reports = new List<UpdateReport>();
        var reporter = new Mock<IUpdateReporter>();
        reporter.Setup(r => r.ReportAsync(It.IsAny<UpdateReport>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateReport, CancellationToken>((report, _) => reports.Add(report))
            .Returns(Task.CompletedTask);
        var strategy = new ReportOnlyStrategy { Reporter = reporter.Object };
        strategy.Create(new UpdateContext
        {
            InstallPath = _root,
            TempPath = tempPath,
            Format = Format.Zip,
            Encoding = Encoding.UTF8,
            DurableUpdateAttemptId = Guid.NewGuid(),
            DisableLegacyRollback = true,
            UpdateVersions = new List<VersionEntry>
            {
                new() { Name = "client-full", Version = "2.0.0", AppType = (int)AppType.Client, PackageType = (int)PackageType.Full },
                new() { Name = "upgrade-chain", Version = "2.0.0", AppType = (int)AppType.Upgrade, PackageType = (int)PackageType.Chain },
                new() { Name = "client-failure", Version = "2.0.0", AppType = (int)AppType.Client, PackageType = (int)PackageType.Full }
            }
        });

        try
        {
            await strategy.ExecuteAsync();
            Assert.DoesNotContain(reports, report => report.Status == (int)UpdateStatus.Success);
            Assert.Contains(reports, report => report.Status == (int)UpdateStatus.Failure);
        }
        finally
        {
            EventManager.Instance.Clear();
        }
    }

    [Fact]
    public async Task Durable_update_strategy_suppresses_its_post_apply_success_report()
    {
        var reports = new List<UpdateReport>();
        var reporter = new Mock<IUpdateReporter>();
        reporter.Setup(r => r.ReportAsync(It.IsAny<UpdateReport>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateReport, CancellationToken>((report, _) => reports.Add(report))
            .Returns(Task.CompletedTask);
        var strategy = new UpdateStrategy { Reporter = reporter.Object };
        strategy.Create(new UpdateContext
        {
            InstallPath = _root,
            DurableUpdateAttemptId = Guid.NewGuid(),
            DisableLegacyRollback = true,
            UpdateVersions = new List<VersionEntry>(),
            LaunchClientAfterUpdate = false
        });

        await strategy.ExecuteAsync();

        Assert.DoesNotContain(reports, report => report.Status == (int)UpdateStatus.Success);
    }

    [Fact]
    public async Task Legacy_update_strategy_keeps_its_success_report()
    {
        var reports = new List<UpdateReport>();
        var reporter = new Mock<IUpdateReporter>();
        reporter.Setup(r => r.ReportAsync(It.IsAny<UpdateReport>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateReport, CancellationToken>((report, _) => reports.Add(report))
            .Returns(Task.CompletedTask);
        var strategy = new UpdateStrategy { Reporter = reporter.Object };
        strategy.Create(new UpdateContext
        {
            InstallPath = _root,
            UpdateVersions = new List<VersionEntry>(),
            LaunchClientAfterUpdate = false
        });

        await strategy.ExecuteAsync();

        Assert.Contains(reports, report => report.Status == (int)UpdateStatus.Success);
    }

    [Fact]
    public async Task Durable_client_update_rejects_custom_strategy_before_backup()
    {
        var installPath = Path.Combine(_root, "install-with-custom");
        Directory.CreateDirectory(installPath);
        var strategy = new ClientStrategy();
        strategy.SetOsStrategy(Mock.Of<IStrategy>());
        strategy.Create(new UpdateContext
        {
            InstallPath = installPath,
            UpdateUrl = "https://example.test/update",
            AppSecretKey = "test-key",
            DurableUpdateAttemptId = Guid.NewGuid()
        });

        try
        {
            await strategy.ExecuteAsync();
            Assert.False(Directory.Exists(DurableUpdateAttemptStore.GetDirectory(installPath)));
            Assert.False(Directory.Exists(Path.Combine(installPath, StorageManager.BackupRootDirectory)));
        }
        finally
        {
            EventManager.Instance.Clear();
        }
    }

    [Fact]
    public void Child_process_requires_the_matching_attempt_contract_and_rejects_missing_or_corrupt_ipc()
    {
        var installPath = Path.Combine(_root, "install-ipc");
        var sourcePath = Path.Combine(_root, "source-ipc");
        var tempRoot = Path.Combine(_root, "ipc-temp");
        var ipcDirectory = Path.Combine(tempRoot, "GeneralUpdate", "ipc");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(ipcDirectory);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "snapshot");
        using (var archive = ZipFile.Open(Path.Combine(_root, "package.zip"), ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("updated.txt").Open()))
            writer.Write("applied-in-child");
        var store = CreateBoundAttempt(installPath, sourcePath, out var attemptId);
        var contract = CreateAttemptContract(installPath, attemptId, store.GetAttempt(attemptId).SnapshotPath!);
        var provider = new EncryptedFileProcessContractProvider(ipcDirectory);
        var ipcPath = EncryptedFileProcessContractProvider.GetDefaultFilePath(ipcDirectory);

        provider.Send(contract);
        var validStart = CreateProbeStart("attempt-apply");
        ConfigureProbeTemp(validStart, tempRoot);
        using (var child = new ProcessStartStrategy().Launch(validStart, attemptId))
        {
            var appliedOutput = Finish(child).Split('|');
            Assert.Equal(attemptId.ToString("D"), appliedOutput[0]);
            Assert.DoesNotContain((int)UpdateStatus.Success, appliedOutput[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
            Assert.Equal("applied-in-child", File.ReadAllText(Path.Combine(installPath, "updated.txt")));
        }

        File.Delete(ipcPath);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));

        File.WriteAllBytes(ipcPath, new byte[] { 1, 2, 3, 4 });
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));

        contract.DurableUpdateAttemptId = Guid.NewGuid();
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));

        contract.DurableUpdateAttemptId = attemptId;
        contract.Bowl = "Bowl.exe";
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
    }

    [Fact]
    public void Competing_process_claims_are_fenced_and_stale_claim_cannot_complete()
    {
        var installPath = Path.Combine(_root, "install");
        var sourcePath = Path.Combine(_root, "source");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "data");
        var store = CreateBoundAttempt(installPath, sourcePath, out var attemptId);

        using var first = StartProbe("attempt-claim", installPath, attemptId.ToString(), "0");
        using var second = StartProbe("attempt-claim", installPath, attemptId.ToString(), "0");
        var firstOutput = Finish(first);
        var secondOutput = Finish(second);
        Assert.Contains("claimed", firstOutput + "|" + secondOutput);
        Assert.Contains("stale", firstOutput + "|" + secondOutput);

        var firstClaim = store.ClaimRecovery(attemptId, 1);
        var replacement = store.ClaimRecovery(attemptId, 2);
        Assert.Throws<InvalidOperationException>(() =>
            store.Complete(attemptId, firstClaim, DurableUpdateAttemptCompletion.RecoveryCompleted));
        Assert.True(store.Complete(attemptId, replacement, DurableUpdateAttemptCompletion.RecoveryCompleted));
        Assert.False(store.Complete(attemptId, replacement, DurableUpdateAttemptCompletion.HealthyConfirmed));
        Assert.Equal(DurableUpdateAttemptStatus.RecoveryCompleted, store.GetAttempt(attemptId).Status);
    }

    [Fact]
    public void A_killed_writer_can_be_reopened_by_a_new_process_with_the_exact_snapshot_path()
    {
        var installPath = Path.Combine(_root, "install");
        var sourcePath = Path.Combine(_root, "source");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "durable");
        var attemptId = Guid.NewGuid();
        var readyPath = Path.Combine(_root, "ready");
        using var writer = StartProbe("attempt-bind-hold", installPath, attemptId.ToString(), sourcePath, readyPath);
        try
        {
            Assert.True(SpinWait.SpinUntil(() => File.Exists(readyPath) || writer.HasExited, TimeSpan.FromSeconds(15)));
            Assert.True(File.Exists(readyPath), "The writer exited before binding its snapshot.");
            writer.Kill();
            Assert.True(writer.WaitForExit(15000));

            var output = RunProbe("attempt-read", installPath, attemptId.ToString());
            var fields = output.Split('|');
            Assert.Equal(nameof(DurableUpdateAttemptStatus.SnapshotBound), fields[0]);
            Assert.True(Directory.Exists(fields[1]));
            Assert.Equal("durable", File.ReadAllText(Path.Combine(fields[1], "file.txt")));
            Assert.Equal("0", fields[2]);
        }
        finally
        {
            if (!writer.HasExited) { writer.Kill(); writer.WaitForExit(); }
        }
    }

    [Fact]
    public void Missing_or_corrupt_snapshot_state_fails_closed_without_latest_backup_fallback()
    {
        var installPath = Path.Combine(_root, "install");
        var sourcePath = Path.Combine(_root, "source");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "data");

        var store = CreateBoundAttempt(installPath, sourcePath, out var attemptId);
        var exactPath = store.GetAttempt(attemptId).SnapshotPath!;
        Directory.Delete(exactPath, true);
        Assert.Throws<DirectoryNotFoundException>(() => store.ClaimRecovery(attemptId, 0));
        Assert.Equal(DurableUpdateAttemptStatus.SnapshotBound, store.GetAttempt(attemptId).Status);

        var another = Guid.NewGuid();
        store.Begin(another);
        var missing = Path.Combine(installPath, StorageManager.BackupRootDirectory, "backup-missing-" + another.ToString("N"));
        store.ReserveSnapshot(another, missing);
        Assert.Throws<DirectoryNotFoundException>(() => store.BindSnapshot(another, missing));

        var statePath = Path.Combine(DurableUpdateAttemptStore.GetDirectory(installPath), attemptId.ToString("N") + ".state");
        File.WriteAllBytes(statePath, new byte[] { 0 });
        Assert.Throws<InvalidDataException>(() => StorageManager.CleanBackup(installPath, keepVersions: 0));
    }

    private ProcessContract CreateAttemptContract(string installPath, Guid attemptId, string snapshotPath, string bowl = "")
    {
        return ConfigurationMapper.MapToProcessContract(new UpdateContext
        {
            MainAppName = "App.exe",
            InstallPath = installPath,
            ClientVersion = "1.0.0",
            LastVersion = "2.0.0",
            AppSecretKey = "test-key",
            BackupDirectory = snapshotPath,
            TempPath = _root,
            Encoding = Encoding.UTF8,
            Format = Format.Zip,
            PatchEnabled = false,
            Bowl = bowl,
            DurableUpdateAttemptId = attemptId
        }, new List<VersionEntry>
        {
            new()
            {
                Name = "package",
                Version = "2.0.0",
                Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(_root, "package.zip")))),
                AppType = (int)AppType.Client,
                PackageType = (int)PackageType.Full
            }
        },
            BlackDefaults.DefaultFormats, BlackDefaults.DefaultFiles, BlackDefaults.DefaultDirectories);
    }

    private static string RunRequiredAttemptProbe(string tempRoot, Guid attemptId)
    {
        var start = CreateProbeStart("attempt-apply");
        ConfigureProbeTemp(start, tempRoot);
        start.EnvironmentVariables[DurableUpdateAttemptStore.RequiredAttemptIdEnvironmentVariable] = attemptId.ToString("D");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start required-attempt probe.");
        return Finish(process);
    }

    private static void ConfigureProbeTemp(ProcessStartInfo start, string tempRoot)
    {
        start.EnvironmentVariables["TEMP"] = tempRoot;
        start.EnvironmentVariables["TMP"] = tempRoot;
    }

    private DurableUpdateAttemptStore CreateBoundAttempt(string installPath, string sourcePath, out Guid attemptId)
    {
        attemptId = Guid.NewGuid();
        var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory,
            "backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + attemptId.ToString("N"));
        var store = new DurableUpdateAttemptStore(installPath);
        store.Begin(attemptId);
        store.ReserveSnapshot(attemptId, snapshotPath);
        StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true);
        store.BindSnapshot(attemptId, snapshotPath);
        return store;
    }

    private static Process StartProbe(params string[] args)
    {
        var start = CreateProbeStart(args);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        return Process.Start(start) ?? throw new InvalidOperationException("Failed to start durable-attempt probe.");
    }

    private static ProcessStartInfo CreateProbeStart(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CoreTest.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CoreTest.deps.json"));
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "StartupHealthProbe.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        return start;
    }

    private static string RunProbe(params string[] args)
    {
        using var process = StartProbe(args);
        return Finish(process);
    }

    private static string Finish(Process process)
    {
        Assert.True(process.WaitForExit(15000), "The durable-attempt probe did not exit.");
        var stderr = process.StandardError.ReadToEnd();
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(stderr);
        return process.StandardOutput.ReadToEnd()
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim() ?? string.Empty;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class ReportOnlyStrategy : AbstractStrategy
    {
        protected override PipelineBuilder BuildPipeline(PipelineContext context)
        {
            if (context.Get<string>("ZipFilePath").Contains("client-failure", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic package failure.");
            return new PipelineBuilder(context);
        }
    }

    private sealed class ProcessStartStrategy : AbstractStrategy
    {
        protected override PipelineBuilder BuildPipeline(PipelineContext context) => new(context);

        public Process Launch(ProcessStartInfo startInfo, Guid attemptId)
        {
            RequiredDurableUpdateAttemptId = attemptId;
            return StartProcess(startInfo) ?? throw new InvalidOperationException("Failed to launch process probe.");
        }
    }
}
