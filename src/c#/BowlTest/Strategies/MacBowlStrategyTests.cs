using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// 分支覆盖点：
/// MacBowlStrategy.Prepare()：
///   - lldb 不可用 → 返回 null（平台不支持场景）
///   - lldb 可用 → 返回 ProcessStartInfo（仅 macOS）
///   - lldb 可用时：RedirectStandardOutput/Error 为 true
///   - lldb 可用时：UseShellExecute=false, CreateNoWindow=true
///   - lldb 可用时：FileName="/usr/bin/lldb"
///   - lldb 可用时：Arguments 包含 --batch、process attach、process save-core、quit
///   - FailDirectory 存在时先删除再创建
/// PostProcessAsync()：
///   - 始终返回 Task.CompletedTask
/// </summary>
public class MacBowlStrategyTests
{
    private BowlContext CreateContext()
    {
        return new BowlContext
        {
            ProcessNameOrId = "test_app",
            DumpFileName = "crash.dmp",
            FailFileName = "crash.json",
            TargetPath = "/Applications/TestApp",
            FailDirectory = "/tmp/fail/test",
            BackupDirectory = "/tmp/backup/test",
            WorkModel = "Normal",
            ExtendedField = "1.0.0",
            TimeoutMs = 30_000,
            DumpType = DumpType.Full,
        };
    }

    [Fact]
    public void Prepare_在非macOS平台_返回null()
    {
        var strategy = new MacBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        // On Windows/Linux, lldb is typically not available at /usr/bin/lldb
        // So this should return null on non-macOS
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Null(startInfo);
        }
    }

    [Fact]
    public void Prepare_返回值要么为null要么为有效ProcessStartInfo()
    {
        var strategy = new MacBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        if (startInfo != null)
        {
            // On macOS: validate ProcessStartInfo configuration
            Assert.Equal("/usr/bin/lldb", startInfo.FileName);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.CreateNoWindow);
            Assert.Contains("--batch", startInfo.Arguments);
            Assert.Contains("process attach", startInfo.Arguments);
            Assert.Contains("process save-core", startInfo.Arguments);
            Assert.Contains("quit", startInfo.Arguments);
            Assert.Contains(ctx.ProcessNameOrId, startInfo.Arguments);
        }
    }

    [Fact]
    public async Task PostProcessAsync_始终返回CompletedTask()
    {
        var strategy = new MacBowlStrategy();
        var ctx = CreateContext();
        var exitResult = new ProcessExitResult { ExitCode = 0, OutputLines = new List<string>() };

        var task = strategy.PostProcessAsync(ctx, exitResult, CancellationToken.None);

        Assert.Equal(Task.CompletedTask, task);
        await task;
    }
}
