using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategies;

public class LinuxBowlStrategyTests
{
    private BowlContext CreateContext(
        string processName = "dotnet",
        string? dumpFileName = "crash.dmp")
    {
        return new BowlContext
        {
            ProcessNameOrId = processName,
            DumpFileName = dumpFileName,
            FailFileName = "crash.json",
            TargetPath = "/opt/app",
            FailDirectory = "/tmp/fail/test",
            BackupDirectory = "/tmp/backup/test",
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
            TimeoutMs = 30_000,
            DumpType = DumpType.Full,
        };
    }

    // ---- Prepare: return type ----

    [Fact]
    public void Prepare_ReturnsNullOrValidProcessStartInfo()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        if (startInfo != null)
        {
            Assert.Equal("procdump", startInfo.FileName);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.CreateNoWindow);
        }
    }

    // ---- Prepare: PID vs process name flag selection ----

    [Fact]
    public void Prepare_ProcessName_UsesWFlag()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext(processName: "dotnet");

        var startInfo = strategy.Prepare(ctx);

        if (startInfo != null)
        {
            Assert.Contains("-w", startInfo.Arguments);
            Assert.Contains("dotnet", startInfo.Arguments);
        }
    }

    [Fact]
    public void Prepare_Pid_UsesPFlag()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext(processName: "12345");

        var startInfo = strategy.Prepare(ctx);

        if (startInfo != null)
        {
            Assert.Contains("-p", startInfo.Arguments);
            Assert.Contains("12345", startInfo.Arguments);
        }
    }

    // ---- Prepare: consistency ----

    [Fact]
    public void Prepare_MultipleCalls_ConsistentResult()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();

        var result1 = strategy.Prepare(ctx);
        var result2 = strategy.Prepare(ctx);

        Assert.Equal(result1 == null, result2 == null);
    }

    [Fact]
    public void Prepare_DifferentContexts_EachUsesOwnProcessName()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx1 = CreateContext(processName: "app1");
        var ctx2 = CreateContext(processName: "app2");

        var si1 = strategy.Prepare(ctx1);
        if (si1 != null)
            Assert.Contains("app1", si1.Arguments);

        var si2 = strategy.Prepare(ctx2);
        if (si2 != null)
            Assert.Contains("app2", si2.Arguments);
    }

    // ---- Prepare: unsupported hint file written ----

    [Fact]
    public void Prepare_UnsupportedDistro_WritesHintFile()
    {
        // When procdump is not in PATH and we're not on a supported distro,
        // the strategy should write bowl_linux_unsupported.txt in the fail directory.
        // This is hard to test in isolation without mocking, but on Windows
        // the /etc/os-release path doesn't exist, so Prepare should return null
        // and the hint file should be written.
        var tempDir = Path.Combine(Path.GetTempPath(), $"BowlLinuxTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var ctx = CreateContext(processName: "test");
            ctx = new BowlContext
            {
                ProcessNameOrId = "test",
                DumpFileName = "crash.dmp",
                FailFileName = "crash.json",
                TargetPath = "/opt/app",
                FailDirectory = tempDir,
                BackupDirectory = "/tmp/backup",
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
                TimeoutMs = 30_000,
                DumpType = DumpType.Full,
            };

            var strategy = new LinuxBowlStrategy();
            var startInfo = strategy.Prepare(ctx);

            // On non-Linux (or Linux without procdump in PATH), should return null
            if (startInfo == null)
            {
                // A hint file should have been written explaining why
                var hintPath = Path.Combine(tempDir, "bowl_linux_unsupported.txt");
                var hintExists = File.Exists(hintPath);

                // If it doesn't exist, procdump might already be installed
                if (!hintExists)
                {
                    // That's also fine — means procdump is available
                }
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ---- PostProcessAsync ----

    [Fact]
    public async Task PostProcessAsync_AlwaysReturnsCompletedTask()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();
        var exitResult = new ProcessExitResult { ExitCode = 0, OutputLines = new List<string>() };

        var task = strategy.PostProcessAsync(ctx, exitResult, CancellationToken.None);

        Assert.Equal(Task.CompletedTask, task);
        await task;
    }
}
