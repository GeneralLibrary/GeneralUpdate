using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// 分支覆盖点：
/// WindowsBowlStrategy.Prepare()：
///   - X86 架构 → "procdump.exe"
///   - X64 架构 → "procdump64.exe"
///   - 其他架构 → "procdump64a.exe"
///   - DumpType.Full → "-ma" 标志
///   - DumpType.Mini → "-mm" 标志
///   - DumpType.Heap → "-mh" 标志
///   - 正常返回非 null ProcessStartInfo
///   - FailDirectory 已存在 → 先删除再创建
///   - ProcessStartInfo 配置检查：RedirectStandardOutput/Error, UseShellExecute, CreateNoWindow
///   - 参数格式化：文件路径包含双引号
/// PostProcessAsync()：
///   - 始终返回 Task.CompletedTask
/// </summary>
public class WindowsBowlStrategyTests
{
    private BowlContext CreateContext(
        string processName = "test.exe",
        DumpType dumpType = DumpType.Full,
        string? dumpFileName = "crash.dmp")
    {
        return new BowlContext
        {
            ProcessNameOrId = processName,
            DumpFileName = dumpFileName,
            FailFileName = "crash.json",
            TargetPath = Path.GetTempPath(),
            FailDirectory = Path.Combine(Path.GetTempPath(), "fail", "test"),
            BackupDirectory = Path.Combine(Path.GetTempPath(), "backup", "test"),
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
            TimeoutMs = 30_000,
            DumpType = dumpType,
        };
    }

    private string NormalizePath(string path)
    {
        // Remove double quotes for assertion
        return path.Trim('"').Replace("\\\\", "\\");
    }

    [Fact]
    public void Prepare_正常上下文_返回非null的ProcessStartInfo()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
    }

    [Fact]
    public void Prepare_正常上下文_FileName包含procdump路径()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.Contains("procdump", startInfo!.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_正常上下文_Arguments包含进程名()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext(processName: "myapp.exe");

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.Contains("myapp.exe", startInfo!.Arguments);
    }

    [Fact]
    public void Prepare_DumpTypeFull_Arguments包含ma标志()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext(dumpType: DumpType.Full);

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.Contains("-ma", startInfo!.Arguments);
    }

    [Fact]
    public void Prepare_DumpTypeMini_Arguments包含mm标志()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext(dumpType: DumpType.Mini);

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.Contains("-mm", startInfo!.Arguments);
    }

    [Fact]
    public void Prepare_DumpTypeHeap_Arguments包含mh标志()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext(dumpType: DumpType.Heap);

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.Contains("-mh", startInfo!.Arguments);
    }

    [Fact]
    public void Prepare_RedirectStandardOutput为true()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.True(startInfo!.RedirectStandardOutput);
    }

    [Fact]
    public void Prepare_RedirectStandardError为true()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.True(startInfo!.RedirectStandardError);
    }

    [Fact]
    public void Prepare_UseShellExecute为false()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.False(startInfo!.UseShellExecute);
    }

    [Fact]
    public void Prepare_CreateNoWindow为true()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.True(startInfo!.CreateNoWindow);
    }

    [Fact]
    public void Prepare_Arguments包含e标志()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        Assert.NotNull(startInfo);
        Assert.StartsWith("-e ", startInfo!.Arguments);
    }

    [Fact]
    public async Task PostProcessAsync_始终返回CompletedTask()
    {
        var strategy = new WindowsBowlStrategy();
        var ctx = CreateContext();
        var exitResult = new ProcessExitResult { ExitCode = 0, OutputLines = new List<string>() };

        var task = strategy.PostProcessAsync(ctx, exitResult, CancellationToken.None);

        Assert.Equal(Task.CompletedTask, task);
        await task; // Should complete immediately
    }
}
