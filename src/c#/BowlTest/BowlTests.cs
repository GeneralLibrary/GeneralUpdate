using System.Diagnostics;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategies;
using GeneralUpdate.Bowl.Strategys;
using BowlTest.Utilities;

/// <summary>
/// Branch coverage points for Bowl:
/// - Constructor with null args throws ArgumentNullException for each parameter
/// - LaunchAsync: strategy.Prepare returns null -> tool unavailable result
/// - LaunchAsync: cancelled token -> OperationCanceledException
/// - LaunchAsync: normal exit, no dump -> Success depends on exitCode
/// - HandleCrashAsync: all steps succeed -> full BowlResult
/// - HandleCrashAsync: GenerateReport fails -> continues
/// - HandleCrashAsync: skip restore when conditions not met
/// - HandleCrashAsync: OnCrash callback with valid path -> invoked
/// - HandleCrashAsync: OnCrash callback throws -> swallowed
/// - MapToContext: all fields mapped correctly
/// - CreateParameter: env var not set/empty/whitespace/invalid -> ArgumentNullException
/// - CreateParameter: valid JSON -> correct MonitorParameter
/// </summary>
public class BowlTests
{
    private readonly FakeBowlStrategy _strategy;
    private readonly FakeCrashReporter _reporter;
    private readonly FakeSystemInfoProvider _sysInfo;
    private readonly FakeEnvironmentProvider _env;

    public BowlTests()
    {
        _strategy = new FakeBowlStrategy();
        _reporter = new FakeCrashReporter();
        _sysInfo = new FakeSystemInfoProvider();
        _env = new FakeEnvironmentProvider();
    }

    private Bowl CreateBowl() =>
        new Bowl(_strategy, _reporter, _sysInfo, _env);

    private static BowlContext CreateValidContext(
        string processName = "test.exe",
        string workModel = "Upgrade",
        bool autoRestore = true,
        string? failDir = null) => new BowlContext
        {
            ProcessNameOrId = processName,
            DumpFileName = "v1_fail.dmp",
            FailFileName = "v1_fail.json",
            TargetPath = Path.GetTempPath(),
            FailDirectory = failDir ?? Path.Combine(Path.GetTempPath(), "fail", "v1"),
            BackupDirectory = Path.Combine(Path.GetTempPath(), "backup", "v1"),
            WorkModel = workModel,
            ExtendedField = "1.0.0",
            TimeoutMs = 30_000,
            DumpType = DumpType.Full,
            AutoRestore = autoRestore,
        };

    // ---- Constructor Tests ----

    [Fact]
    public void Constructor_AllArgsValid_DoesNotThrow()
    {
        var ex = Record.Exception(() => CreateBowl());
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_StrategyNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new Bowl(null!, _reporter, _sysInfo, _env));
        Assert.Equal("strategy", ex.ParamName);
    }

    [Fact]
    public void Constructor_CrashReporterNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new Bowl(_strategy, null!, _sysInfo, _env));
        Assert.Equal("crashReporter", ex.ParamName);
    }

    [Fact]
    public void Constructor_SystemInfoProviderNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new Bowl(_strategy, _reporter, null!, _env));
        Assert.Equal("systemInfoProvider", ex.ParamName);
    }

    [Fact]
    public void Constructor_EnvNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new Bowl(_strategy, _reporter, _sysInfo, null!));
        Assert.Equal("env", ex.ParamName);
    }

    // ---- LaunchAsync: Strategy returns null ----

    [Fact]
    public async Task LaunchAsync_StrategyReturnsNull_ReturnsFailedResult()
    {
        _strategy.PrepareResult = null;
        var bowl = CreateBowl();
        var ctx = CreateValidContext();

        var result = await bowl.LaunchAsync(ctx);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.False(result.DumpCaptured);
        Assert.Null(result.DumpFilePath);
        Assert.Null(result.CrashReportPath);
        Assert.False(result.Restored);
        Assert.True(_strategy.PrepareCalled);
    }

    // ---- LaunchAsync: Cancelled ----

    [Fact]
    public async Task LaunchAsync_CancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        _strategy.PrepareResult = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout 10",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var bowl = CreateBowl();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            bowl.LaunchAsync(CreateValidContext(), cts.Token));
    }

    // ---- LaunchAsync: ProcessRunner timeout ----

    [Fact]
    public async Task LaunchAsync_ProcessTimeout_ReturnsFailedResult()
    {
        // Use a short timeout to trigger timeout
        _strategy.PrepareResult = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout 5",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var bowl = CreateBowl();
        var ctx = CreateValidContext();
        ctx = ctx.Normalize();
        ctx = new BowlContext
        {
            ProcessNameOrId = ctx.ProcessNameOrId,
            DumpFileName = ctx.DumpFileName,
            FailFileName = ctx.FailFileName,
            TargetPath = ctx.TargetPath,
            FailDirectory = ctx.FailDirectory,
            BackupDirectory = ctx.BackupDirectory,
            WorkModel = ctx.WorkModel,
            ExtendedField = ctx.ExtendedField,
            TimeoutMs = 1, // Force immediate timeout
            DumpType = ctx.DumpType,
            AutoRestore = ctx.AutoRestore,
        };

        var result = await bowl.LaunchAsync(ctx);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.False(result.DumpCaptured);
    }

    // ---- LaunchAsync: Normal exit, no dump ----

    [Fact]
    public async Task LaunchAsync_NormalExit_NoDump_SuccessIsExitCodeZero()
    {
        _strategy.PrepareResult = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var bowl = CreateBowl();
        var failDir = Path.Combine(Path.GetTempPath(), $"BowlTest_NoDump_{Guid.NewGuid():N}");
        Directory.CreateDirectory(failDir);
        try
        {
            var ctx = CreateValidContext(failDir: failDir);
            var result = await bowl.LaunchAsync(ctx);
            Assert.True(result.Success);
            Assert.False(result.DumpCaptured);
        }
        finally
        {
            try { Directory.Delete(failDir, recursive: true); } catch { }
        }
    }

    // ---- LaunchAsync: Crash detected (dump file exists) ----

    [Fact]
    public async Task LaunchAsync_CrashDetected_HandleCrashInvoked()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_Crash_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _reporter.ReportPath = Path.Combine(failDir, "v1_fail.json");

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = true,
            };

            var result = await bowl.LaunchAsync(ctx);

            Assert.False(result.Success);
            Assert.True(result.DumpCaptured);
            Assert.NotNull(result.DumpFilePath);
            Assert.True(_reporter.GenerateReportCalled);
            Assert.True(_sysInfo.ExportCalled);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    // ---- MapToContext ----

    [Fact]
    public void MapToContext_ConvertsAllFields_CorrectValues()
    {
        var param = new MonitorParameter
        {
            ProcessNameOrId = "myapp.exe",
            DumpFileName = "v2_fail.dmp",
            FailFileName = "v2_fail.json",
            TargetPath = "C:\\app",
            FailDirectory = "C:\\app\\fail\\v2",
            BackupDirectory = "C:\\app\\v2",
            WorkModel = "Normal",
            ExtendedField = "2.0.0",
        };

        var ctx = Bowl.MapToContext(param);

        Assert.Equal("myapp.exe", ctx.ProcessNameOrId);
        Assert.Equal("v2_fail.dmp", ctx.DumpFileName);
        Assert.Equal("v2_fail.json", ctx.FailFileName);
        Assert.Equal("C:\\app", ctx.TargetPath);
        Assert.Equal("C:\\app\\fail\\v2", ctx.FailDirectory);
        Assert.Equal("C:\\app\\v2", ctx.BackupDirectory);
        Assert.Equal("Normal", ctx.WorkModel);
        Assert.Equal("2.0.0", ctx.ExtendedField);
        Assert.Equal(30_000, ctx.TimeoutMs);
        Assert.Equal(DumpType.Full, ctx.DumpType);
        Assert.True(ctx.AutoRestore);
    }

    [Fact]
    public void MapToContext_TimeoutMsFixedAt30000()
    {
        var param = new MonitorParameter { ProcessNameOrId = "test" };
        var ctx = Bowl.MapToContext(param);
        Assert.Equal(30_000, ctx.TimeoutMs);
    }

    [Fact]
    public void MapToContext_DumpTypeFixedAtFull()
    {
        var param = new MonitorParameter { ProcessNameOrId = "test" };
        var ctx = Bowl.MapToContext(param);
        Assert.Equal(DumpType.Full, ctx.DumpType);
    }

    [Fact]
    public void MapToContext_AutoRestoreFixedAtTrue()
    {
        var param = new MonitorParameter { ProcessNameOrId = "test" };
        var ctx = Bowl.MapToContext(param);
        Assert.True(ctx.AutoRestore);
    }

    // ---- CreateParameter: Env var not set ----

    [Fact]
    public void CreateParameter_EnvVarNotSet_ThrowsArgumentNullException()
    {
        var oldValue = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo", null, EnvironmentVariableTarget.Process);
            var ex = Assert.Throws<ArgumentNullException>(() => Bowl.CreateParameter());
            Assert.Contains("ProcessInfo", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProcessInfo", oldValue, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void CreateParameter_EnvVarEmpty_ThrowsArgumentNullException()
    {
        var oldValue = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo", string.Empty, EnvironmentVariableTarget.Process);
            var ex = Assert.Throws<ArgumentNullException>(() => Bowl.CreateParameter());
            Assert.Contains("ProcessInfo", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProcessInfo", oldValue, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void CreateParameter_EnvVarWhitespace_ThrowsArgumentNullException()
    {
        var oldValue = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo", "   ", EnvironmentVariableTarget.Process);
            var ex = Assert.Throws<ArgumentNullException>(() => Bowl.CreateParameter());
            Assert.Contains("ProcessInfo", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProcessInfo", oldValue, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void CreateParameter_InvalidJson_ThrowsArgumentNullException()
    {
        var oldValue = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo", "not valid json", EnvironmentVariableTarget.Process);
            var ex = Assert.Throws<ArgumentNullException>(() => Bowl.CreateParameter());
            Assert.Contains("ProcessInfo", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProcessInfo", oldValue, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void CreateParameter_ValidJson_ReturnsCorrectMonitorParameter()
    {
        var oldValue = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.Process);
        var tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_App_{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo",
                $"{{\"AppName\":\"myapp.exe\",\"LastVersion\":\"2.5.0\",\"InstallPath\":\"{tempDir.Replace("\\", "\\\\")}\"}}",
                EnvironmentVariableTarget.Process);

            var param = Bowl.CreateParameter();

            Assert.Equal("myapp.exe", param.ProcessNameOrId);
            Assert.Equal("2.5.0_fail.dmp", param.DumpFileName);
            Assert.Equal("2.5.0_fail.json", param.FailFileName);
            Assert.Equal(tempDir, param.TargetPath);
            Assert.Equal("2.5.0", param.ExtendedField);
            Assert.Equal(Path.Combine(tempDir, "fail", "2.5.0"), param.FailDirectory);
            Assert.Equal(Path.Combine(tempDir, "2.5.0"), param.BackupDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProcessInfo", oldValue, EnvironmentVariableTarget.Process);
        }
    }

    // ---- HandleCrashAsync: All steps success ----

    [Fact]
    public async Task HandleCrashAsync_AllStepsSucceed_ReturnsFullBowlResult()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_HCA_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump content");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _reporter.ReportPath = Path.Combine(failDir, "v1_fail.json");

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = true,
            };

            var result = await bowl.LaunchAsync(ctx);

            Assert.True(result.DumpCaptured);
            Assert.True(_reporter.GenerateReportCalled);
            Assert.True(_sysInfo.ExportCalled);
            Assert.True(_strategy.PostProcessCalled);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    // ---- HandleCrashAsync: Report generation fails ----

    [Fact]
    public async Task HandleCrashAsync_ReportGenerationFails_ContinuesOtherSteps()
    {
        _reporter.GenerateReportException = new IOException("Simulated disk error");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_HCA2_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = false,
            };

            var result = await bowl.LaunchAsync(ctx);

            Assert.True(result.DumpCaptured);
            // Export should still be called despite report failure
            Assert.True(_sysInfo.ExportCalled);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    // ---- HandleCrashAsync: Skip restore conditions ----

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Normal", false)]
    [InlineData("Upgrade", false)]
    public async Task HandleCrashAsync_SkipRestoreConditions(string workModel, bool autoRestore)
    {
        _reporter.ReportPath = "/tmp/report.json";

        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_Restore_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = workModel,
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = autoRestore,
            };

            var result = await bowl.LaunchAsync(ctx);

            Assert.True(result.DumpCaptured);
            // For Normal mode, UpgradeFail should NOT be set
            if (workModel != "Upgrade")
            {
                Assert.False(_env.SetVariableCalled);
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    // ---- HandleCrashAsync: OnCrash callback ----

    [Fact]
    public async Task HandleCrashAsync_OnCrashCallbackValid_Invoked()
    {
        CrashInfo? capturedInfo = null;

        _reporter.ReportPath = "/tmp/report.json";

        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_CB_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = false,
                OnCrash = (info, ct) =>
                {
                    capturedInfo = info;
                    return Task.CompletedTask;
                },
            };

            var result = await bowl.LaunchAsync(ctx);

            Assert.True(result.DumpCaptured);
            Assert.NotNull(capturedInfo);
            Assert.Equal("1.0.0", capturedInfo!.Value.Version);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    // ---- HandleCrashAsync: OnCrash callback throws ----

    [Fact]
    public async Task HandleCrashAsync_OnCrashCallbackThrows_DoesNotInterrupt()
    {
        _reporter.ReportPath = "/tmp/report.json";

        var tempRoot = Path.Combine(Path.GetTempPath(), $"BowlTest_CBErr_{Guid.NewGuid():N}");
        var failDir = Path.Combine(tempRoot, "fail", "v1");
        Directory.CreateDirectory(failDir);
        var dumpPath = Path.Combine(failDir, "v1_fail.dmp");
        File.WriteAllText(dumpPath, "fake dump");

        try
        {
            _strategy.PrepareResult = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bowl = CreateBowl();
            var ctx = new BowlContext
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = tempRoot,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(tempRoot, "backup"),
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
                AutoRestore = false,
                OnCrash = (_, _) => throw new InvalidOperationException("Callback failed"),
            };

            // Should not throw - callback exceptions are swallowed
            var result = await bowl.LaunchAsync(ctx);

            Assert.True(result.DumpCaptured);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}
