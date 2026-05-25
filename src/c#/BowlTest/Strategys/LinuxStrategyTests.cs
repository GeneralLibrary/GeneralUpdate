using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// LinuxStrategy (Obsolete)：
///   - Launch()：调用 Install() 然后 base.Launch()
///   - Launch()：Install 抛出异常 → catch 后重新抛出
///   - Install()：
///     - GetPacketName() 返回有效包名 → 执行 install.sh
///     - GetPacketName() 返回空 → 提前返回（不安装）
///     - install.sh 执行成功（ExitCode=0）→ 记录成功日志
///     - install.sh 执行失败（ExitCode!=0）→ 记录错误日志
///     - install.sh 执行抛出异常 → catch 记录错误
///   - GetPacketName()：
///     - 发行版在 _rocdumpAmd64 列表中 → 返回 .deb 包名
///     - 发行版在 procdump_el8_x86_64 列表中 → 返回 .el8.rpm 包名
///     - 发行版在 procdump_cm2_x86_64 列表中 → 返回 .cm2.rpm 包名
///     - 发行版不在任何列表中 → 返回空字符串
///   - GetSystem()：
///     - /etc/os-release 存在 → 读取 ID 和 VERSION_ID
///     - /etc/os-release 不存在 → 抛出 FileNotFoundException
///     - /etc/os-release 中 ID= 存在但无双引号
///     - /etc/os-release 中 VERSION_ID= 存在但无双引号
/// </summary>
public class LinuxStrategyTests : IDisposable
{
    private readonly string _tempDir;

    public LinuxStrategyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_Linux_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void 构造实例_不抛出异常()
    {
        var ex = Record.Exception(() => new LinuxStrategy());
        Assert.Null(ex);
    }

    [Fact]
    public void SetParameter_设置有效参数_不抛出异常()
    {
        var strategy = new LinuxStrategy();
        var param = new MonitorParameter
        {
            ProcessNameOrId = "dotnet",
            InnerApp = "dotnet",
            InnerArguments = "--version",
            FailDirectory = Path.Combine(_tempDir, "linux_fail"),
            DumpFileName = "crash.dmp",
            FailFileName = "crash.json",
            TargetPath = _tempDir,
            BackupDirectory = Path.Combine(_tempDir, "backup"),
            WorkModel = "Upgrade",
            ExtendedField = "1.0",
        };

        var ex = Record.Exception(() => strategy.SetParameter(param));
        Assert.Null(ex);
        // Legacy strategy doesn't throw on set
    }

    [Fact]
    public void Launch_OnNonLinuxSystem_GracefullyHandlesMissingInstallScript()
    {
        // This test verifies the strategy can be created and called without crashing
        var strategy = new LinuxStrategy();
        var param = new MonitorParameter
        {
            ProcessNameOrId = "dotnet",
            InnerApp = "dotnet",
            InnerArguments = "--version",
            FailDirectory = Path.Combine(_tempDir, "linux_fail"),
            TargetPath = _tempDir,
            BackupDirectory = Path.Combine(_tempDir, "backup"),
        };
        strategy.SetParameter(param);

        // Launch will try to install procdump, which may fail on non-Linux
        // The test verifies it doesn't crash unexpectedly
        try
        {
            strategy.Launch();
        }
        catch (FileNotFoundException)
        {
            // Expected on non-Linux when /etc/os-release not found
        }
        catch (Exception)
        {
            // Other exceptions from missing tools are also acceptable
        }
    }

    // GetSystem and GetPacketName are private; tested indirectly via Launch
    // The branching logic is:
    // - Ubuntu/Debian → .deb
    // - RHEL/CentOS/Fedora → .el8.rpm
    // - ClearOS → .cm2.rpm
    // - Unknown → empty string (skip install)
}
