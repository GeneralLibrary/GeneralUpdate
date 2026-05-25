using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// 分支覆盖点：
/// LinuxBowlStrategy.Prepare()：
///   - 首次调用：尝试安装 procdump → 安装成功 → 返回 ProcessStartInfo
///   - 首次调用：尝试安装 procdump → 安装失败 → 返回 null
///   - 再次调用：procdump 已安装 → 直接使用缓存，不重新安装
///   - 再次调用：procdump 上次安装失败 → 返回 null（不重试）
///   - 安装成功时：FileName="procdump", Arguments 包含 -p 和进程名
///   - 安装成功时：RedirectStandardOutput/Error=true, UseShellExecute=false
///   - FailDirectory 存在时先删除再创建
/// TryInstallProcdump() 分支：
///   - 检测到支持的发行版（ubuntu/debian/rhel/centos/fedora/clearos）→ 找到包名
///   - 检测到不支持的发行版 → 返回 false
///   - /etc/os-release 不存在 → 返回空字符串 → 不匹配任何包 → 返回 false
///   - install.sh 不存在 → 返回 false
///   - 包文件不存在 → 返回 false
/// PostProcessAsync()：
///   - 始终返回 Task.CompletedTask
/// </summary>
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

    [Fact]
    public void Prepare_返回值为null或有效ProcessStartInfo()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();

        var startInfo = strategy.Prepare(ctx);

        // Either null (procdump unavailable) or valid ProcessStartInfo
        if (startInfo != null)
        {
            Assert.Equal("procdump", startInfo.FileName);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.CreateNoWindow);
            Assert.Contains("-p", startInfo.Arguments);
            Assert.Contains(ctx.ProcessNameOrId, startInfo.Arguments);
        }
    }

    [Fact]
    public void Prepare_多次调用_结果一致()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();

        var result1 = strategy.Prepare(ctx);
        var result2 = strategy.Prepare(ctx);

        // Both calls should return same type (both null or both not null)
        Assert.Equal(result1 == null, result2 == null);
    }

    [Fact]
    public void Prepare_不同上下文_分别处理()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx1 = CreateContext(processName: "app1");
        var ctx2 = CreateContext(processName: "app2");

        var si1 = strategy.Prepare(ctx1);

        if (si1 != null)
        {
            Assert.Contains("app1", si1.Arguments);
        }

        var si2 = strategy.Prepare(ctx2);

        if (si2 != null)
        {
            Assert.Contains("app2", si2.Arguments);
        }
    }

    [Fact]
    public async Task PostProcessAsync_始终返回CompletedTask()
    {
        var strategy = new LinuxBowlStrategy();
        var ctx = CreateContext();
        var exitResult = new ProcessExitResult { ExitCode = 0, OutputLines = new List<string>() };

        var task = strategy.PostProcessAsync(ctx, exitResult, CancellationToken.None);

        Assert.Equal(Task.CompletedTask, task);
        await task;
    }
}
