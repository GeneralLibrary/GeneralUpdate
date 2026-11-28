using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
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
using Microsoft.Data.Sqlite;

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
        Assert.Null(legacyContract.SnapshotManifestHash);
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
            DurableUpdateAttemptId = attemptId,
            SnapshotManifestHash = new string('A', 64)
        };

        var contract = ConfigurationMapper.MapToProcessContract(
            source, new List<VersionEntry> { new() { Version = "2.0.0" } },
            new List<string>(), new List<string>(), new List<string>());
        var json = JsonSerializer.Serialize(contract, ProcessContractJsonContext.Default.ProcessContract);
        var reopened = JsonSerializer.Deserialize(json, ProcessContractJsonContext.Default.ProcessContract)!;

        Assert.Equal(attemptId, reopened.DurableUpdateAttemptId);
        Assert.Equal(source.SnapshotManifestHash, reopened.SnapshotManifestHash);
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
        StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true,
            rejectReparsePoints: true);
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
    public void Strict_backup_and_bound_snapshot_reject_directory_links()
    {
        var installPath = Path.Combine(_root, "link-install");
        var sourcePath = Path.Combine(_root, "link-source");
        var externalPath = Path.Combine(_root, "outside");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(externalPath);
        File.WriteAllText(Path.Combine(externalPath, "outside.txt"), "outside");
        var link = Path.Combine(sourcePath, "linked");
        CreateDirectoryLink(link, externalPath);
        try
        {
            var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory, "backup-link");
            Assert.Throws<InvalidDataException>(() =>
                StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true,
                    rejectReparsePoints: true));
            Assert.False(Directory.Exists(snapshotPath));
        }
        finally
        {
            Directory.Delete(link);
        }

        File.WriteAllText(Path.Combine(sourcePath, "data.txt"), "data");
        var store = CreateBoundAttempt(installPath, sourcePath, out var attemptId);
        var record = store.GetAttempt(attemptId);
        var snapshotLink = Path.Combine(record.SnapshotPath!, "linked");
        CreateDirectoryLink(snapshotLink, externalPath);
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                store.VerifySnapshotContent(attemptId, record.SnapshotPath!, record.SnapshotManifestHash!));
        }
        finally
        {
            Directory.Delete(snapshotLink);
        }
    }

    [Fact]
    public async Task Client_strategy_creates_and_binds_the_backup_used_by_its_update_workflow()
    {
        var installPath = Path.Combine(_root, "install");
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "app-state.txt"), "before-update");
        var attemptId = Guid.NewGuid();
        var packageFixture = Path.Combine(_root, "client-package.zip");
        using (var archive = ZipFile.Open(packageFixture, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("updated.txt").Open()))
            writer.Write("child-applied");
        byte[] packageHash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            packageHash = sha.ComputeHash(File.ReadAllBytes(packageFixture));
        var asset = new DownloadAsset("client", "https://example.test/client.zip",
            new FileInfo(packageFixture).Length, Convert.ToHexString(packageHash), "2.0.0",
            AppType: (int)AppType.Client, PackageType: (int)PackageType.Full);
        var chain = new DownloadAsset("chain", "https://example.test/chain.zip",
            asset.Size, null, "2.0.0", AppType: (int)AppType.Client, PackageType: (int)PackageType.Chain);
        var source = new Mock<IDownloadSource>();
        source.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadSourceResult { Assets = new[] { chain, asset }, HasMainUpdate = true });
        var executor = new Mock<IDownloadExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<DownloadAsset>(), It.IsAny<string>(),
                It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()))
            .Returns((DownloadAsset download, string path, IProgress<DownloadProgress>? _, CancellationToken _) =>
            {
                File.Copy(packageFixture, path, overwrite: true);
                return Task.FromResult(new DownloadResult(download, path,
                    new FileInfo(path).Length, TimeSpan.Zero, 0, true, null));
            });
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
            ProcessContractProvider = new EncryptedFileProcessContractProvider(
                Path.Combine(_root, "GeneralUpdate", "ipc"))
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
            Assert.True(strategy.HasPreparedClientUpdate,
                $"Client update was not prepared. Reports: {string.Join(",", reports.Select(report => report.Status))}");
            var attempt = new DurableUpdateAttemptStore(installPath).GetAttempt(attemptId);
            Assert.Equal(DurableUpdateAttemptStatus.SnapshotBound, attempt.Status);
            Assert.NotNull(attempt.SnapshotPath);
            Assert.Contains(attemptId.ToString("N"), Path.GetFileName(attempt.SnapshotPath));
            Assert.NotNull(attempt.SnapshotManifestHash);
            Assert.Equal("before-update", File.ReadAllText(Path.Combine(attempt.SnapshotPath, "app-state.txt")));
            Assert.DoesNotContain(reports, report => report.Status == (int)UpdateStatus.Success);

            var childStart = CreateProbeStart("attempt-apply");
            ConfigureProbeTemp(childStart, _root);
            childStart.EnvironmentVariables[DurableUpdateAttemptStore.RequiredAttemptIdEnvironmentVariable] =
                attemptId.ToString("D");
            using var child = Process.Start(childStart) ?? throw new InvalidOperationException("Failed to start update child.");
            var appliedOutput = Finish(child).Split('|');
            Assert.Equal(attemptId.ToString("D"), appliedOutput[0]);
            Assert.DoesNotContain((int)UpdateStatus.Success, appliedOutput[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
            var appliedFile = Path.Combine(installPath, "updated.txt");
            Assert.True(File.Exists(appliedFile), $"Child output: {string.Join("|", appliedOutput)}");
            Assert.Equal("child-applied", File.ReadAllText(appliedFile));
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

        provider.Send(contract);
        var snapshotPath = store.GetAttempt(attemptId).SnapshotPath!;
        var snapshotFile = Path.Combine(snapshotPath, "file.txt");
        File.WriteAllText(snapshotFile, "SNAPSHOT");
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        File.WriteAllText(snapshotFile, "snapshot");
        File.Delete(snapshotFile);
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        File.WriteAllText(snapshotFile, "snapshot");
        File.WriteAllText(Path.Combine(snapshotPath, "unexpected.txt"), "extra");
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        File.Delete(Path.Combine(snapshotPath, "unexpected.txt"));

        var manifestPath = DurableUpdateAttemptStore.GetSnapshotManifestPath(installPath, attemptId);
        var manifest = File.ReadAllBytes(manifestPath);
        File.WriteAllBytes(manifestPath, new byte[] { 0, 1, 2, 3 });
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        File.WriteAllBytes(manifestPath, manifest);

        contract.SnapshotManifestHash = new string('0', 64);
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        contract.SnapshotManifestHash = null;
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        contract.SnapshotManifestHash = store.GetAttempt(attemptId).SnapshotManifestHash;

        contract.DurableUpdateAttemptId = Guid.NewGuid();
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));

        contract.DurableUpdateAttemptId = attemptId;
        contract.Bowl = "Bowl.exe";
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
    }

    [Fact]
    public async Task Child_apply_failure_survives_restart_and_replays_exact_ordered_event_over_http()
    {
        var installPath = Path.Combine(_root, "install-ordered");
        var sourcePath = Path.Combine(_root, "source-ordered");
        var tempRoot = Path.Combine(_root, "ipc-ordered");
        var ipcDirectory = Path.Combine(tempRoot, "GeneralUpdate", "ipc");
        var queue = Path.Combine(_root, "queue");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(ipcDirectory);
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "snapshot");
        File.WriteAllText(Path.Combine(_root, "package.zip"), "invalid archive");
        var store = CreateBoundAttempt(installPath, sourcePath, out var attemptId);
        var contract = CreateAttemptContract(installPath, attemptId, store.GetAttempt(attemptId).SnapshotPath!);
        contract.UpdateVersions[0].RecordId = 303;
        contract.ReportType = 2;
        new EncryptedFileProcessContractProvider(ipcDirectory).Send(contract);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var server = new HttpListener();
        var url = $"http://127.0.0.1:{port}/report/";
        server.Prefixes.Add(url);
        server.Start();

        async Task<string> RespondAsync(int status)
        {
            var request = await server.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
            using var reader = new StreamReader(request.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            request.Response.StatusCode = status;
            var bytes = Encoding.UTF8.GetBytes("{\"code\":200,\"body\":true}");
            request.Response.ContentLength64 = bytes.Length;
            await request.Response.OutputStream.WriteAsync(bytes);
            request.Response.Close();
            return body;
        }

        var firstResponse = RespondAsync(503);
        var start = CreateProbeStart("attempt-apply-ordered", queue, url);
        ConfigureProbeTemp(start, tempRoot);
        start.EnvironmentVariables[DurableUpdateAttemptStore.RequiredAttemptIdEnvironmentVariable] = attemptId.ToString("D");
        string failedDelivery;
        using (var child = Process.Start(start)!)
        try
        {
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            failedDelivery = await firstResponse;
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(child.ExitCode == 0, await stderr);
            Assert.Contains("applied", await stdout);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(); }
        }
        using (var db = new SqliteConnection($"Data Source={Path.Combine(queue, "ordered-reports.db")};Mode=ReadOnly;Pooling=False"))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT payload FROM outbox ORDER BY id;";
            using var records = command.ExecuteReader();
            Assert.True(records.Read());
            Assert.Equal(failedDelivery, records.GetString(0));
            Assert.False(records.Read());
        }

        var secondResponse = RespondAsync(200);
        string replay;
        using (var restarted = StartProbe("attempt-replay-ordered", queue, url))
        try
        {
            var stdout = restarted.StandardOutput.ReadToEndAsync();
            var stderr = restarted.StandardError.ReadToEndAsync();
            replay = await secondResponse;
            await restarted.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(restarted.ExitCode == 0, await stderr);
            Assert.Contains("replayed", await stdout);
        }
        finally
        {
            if (!restarted.HasExited) { restarted.Kill(entireProcessTree: true); restarted.WaitForExit(); }
        }
        Assert.Equal(failedDelivery, replay);
        using var document = JsonDocument.Parse(replay);
        var report = document.RootElement;
        Assert.Equal(303, report.GetProperty("recordId").GetInt32());
        Assert.Equal(2, report.GetProperty("type").GetInt32());
        Assert.Equal((int)UpdateStatus.Failure, report.GetProperty("status").GetInt32());
        Assert.Equal((int)FailureCategory.Extract, report.GetProperty("category").GetInt32());
        Assert.Equal("Extract", report.GetProperty("phase").GetString());
        Assert.Equal(1, report.GetProperty("sequence").GetInt64());
        Assert.NotEqual(Guid.Empty, report.GetProperty("eventId").GetGuid());
        using var reopened = new SqliteConnection($"Data Source={Path.Combine(queue, "ordered-reports.db")};Mode=ReadOnly;Pooling=False");
        reopened.Open();
        using var pending = reopened.CreateCommand();
        pending.CommandText = "SELECT COUNT(*) FROM outbox;";
        Assert.Equal(0L, (long)pending.ExecuteScalar()!);
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

    [Fact]
    public void Legacy_v1_bound_snapshot_is_still_pinned_but_cannot_be_promoted_or_verified()
    {
        var installPath = Path.Combine(_root, "legacy-install");
        var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory, "backup-legacy-attempt");
        var tempRoot = Path.Combine(_root, "legacy-ipc");
        Directory.CreateDirectory(snapshotPath);
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(snapshotPath, "file.txt"), "legacy");
        using (var archive = ZipFile.Open(Path.Combine(_root, "package.zip"), ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("updated.txt").Open()))
            writer.Write("must-not-apply");
        var attemptId = Guid.NewGuid();
        WriteLegacyBoundAttempt(installPath, attemptId, snapshotPath);
        var store = new DurableUpdateAttemptStore(installPath);

        var record = store.GetAttempt(attemptId);
        Assert.Equal(DurableUpdateAttemptStatus.SnapshotBound, record.Status);
        Assert.Null(record.SnapshotManifestHash);
        Assert.Throws<InvalidDataException>(() =>
            store.VerifySnapshotContent(attemptId, snapshotPath, new string('A', 64)));
        Assert.Throws<InvalidDataException>(() => store.ClaimRecovery(attemptId, 0));
        var contract = CreateAttemptContract(installPath, attemptId, snapshotPath);
        contract.SnapshotManifestHash = new string('A', 64);
        var provider = new EncryptedFileProcessContractProvider(Path.Combine(tempRoot, "GeneralUpdate", "ipc"));
        provider.Send(contract);
        Assert.Equal("rejected", RunRequiredAttemptProbe(tempRoot, attemptId));
        Assert.False(File.Exists(Path.Combine(installPath, "updated.txt")));

        StorageManager.CleanBackup(installPath, keepVersions: 0);

        Assert.True(Directory.Exists(snapshotPath));
        Assert.Null(store.GetAttempt(attemptId).SnapshotManifestHash);
    }

    private ProcessContract CreateAttemptContract(string installPath, Guid attemptId, string snapshotPath, string bowl = "")
    {
        var contract = ConfigurationMapper.MapToProcessContract(new UpdateContext
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
        contract.SnapshotManifestHash = new DurableUpdateAttemptStore(installPath).GetAttempt(attemptId)
            .SnapshotManifestHash;
        return contract;
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
        start.EnvironmentVariables["TMPDIR"] = tempRoot;
    }

    private DurableUpdateAttemptStore CreateBoundAttempt(string installPath, string sourcePath, out Guid attemptId)
    {
        attemptId = Guid.NewGuid();
        var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory,
            "backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + attemptId.ToString("N"));
        var store = new DurableUpdateAttemptStore(installPath);
        store.Begin(attemptId);
        store.ReserveSnapshot(attemptId, snapshotPath);
        StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true,
            rejectReparsePoints: true);
        store.BindSnapshot(attemptId, snapshotPath);
        return store;
    }

    private static void WriteLegacyBoundAttempt(string installPath, Guid attemptId, string snapshotPath)
    {
        var journalDirectory = DurableUpdateAttemptStore.GetDirectory(installPath);
        Directory.CreateDirectory(journalDirectory);
        byte[] payload;
        using (var memory = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write(attemptId.ToByteArray());
                writer.Write((int)DurableUpdateAttemptStatus.SnapshotBound);
                writer.Write(0L);
                writer.Write(false);
                writer.Write(true);
                writer.Write(snapshotPath);
            }
            payload = memory.ToArray();
        }

        byte[] hash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            hash = sha.ComputeHash(payload);
        var bytes = new byte[payload.Length + hash.Length];
        Buffer.BlockCopy(payload, 0, bytes, 0, payload.Length);
        Buffer.BlockCopy(hash, 0, bytes, payload.Length, hash.Length);
        File.WriteAllBytes(Path.Combine(journalDirectory, attemptId.ToString("N") + ".state"), bytes);
    }

    private static void CreateDirectoryLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to create junction.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"Failed to create junction: {stdout} {stderr}");
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
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill();
            process.WaitForExit();
            throw new Xunit.Sdk.XunitException(
                $"The durable-attempt probe did not exit. stdout: {standardOutput.GetAwaiter().GetResult()} " +
                $"stderr: {standardError.GetAwaiter().GetResult()}");
        }
        var stderr = standardError.GetAwaiter().GetResult();
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(stderr);
        return standardOutput.GetAwaiter().GetResult()
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
