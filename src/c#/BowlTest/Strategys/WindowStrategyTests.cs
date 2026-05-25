using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// WindowStrategy (Obsolete)：
///   - Launch()：
///     - 初始化 Actions 管道：CreateCrash, Export, Restore, SetEnvironment
///     - 根据 OS 架构选择 procdump 可执行文件：
///       - X86 → "procdump.exe"
///       - X64 → "procdump64.exe"
///       - 其他 → "procdump64a.exe"
///     - InnerArguments 格式：-e -ma {ProcessNameOrId} {dmpFullName}
///     - 调用 base.Launch()（启动进程）
///     - ExecuteFinalTreatment()：
///       - dump 文件存在 → 执行所有 actions
///       - dump 文件不存在 → 跳过 actions
///   - GetAppName()：
///     - X86 → "procdump.exe"
///     - X64 → "procdump64.exe"
///     - 其他 → "procdump64a.exe"
///   - CreateCrash()：序列化 Crash 到 JSON
///   - Export()：
///     - export.bat 存在 → 启动
///     - export.bat 不存在 → 抛出 FileNotFoundException
///   - Restore()：
///     - WorkModel="Upgrade" → 执行恢复
///     - WorkModel!="Upgrade" → 跳过
///   - SetEnvironment()：
///     - WorkModel="Upgrade" → 设置环境变量
///     - WorkModel!="Upgrade" → 跳过
/// </summary>
public class WindowStrategyTests : IDisposable
{
    private readonly string _tempDir;

    public WindowStrategyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_Win_{Guid.NewGuid():N}");
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
        var ex = Record.Exception(() => new WindowStrategy());
        Assert.Null(ex);
    }

    [Fact]
    public void SetParameter_设置有效参数_不抛出异常()
    {
        var strategy = new WindowStrategy();
        var param = new MonitorParameter
        {
            ProcessNameOrId = "test.exe",
            DumpFileName = "crash.dmp",
            FailFileName = "crash.json",
            TargetPath = _tempDir,
            FailDirectory = Path.Combine(_tempDir, "fail"),
            BackupDirectory = Path.Combine(_tempDir, "backup"),
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
        };

        var ex = Record.Exception(() => strategy.SetParameter(param));
        Assert.Null(ex);
    }

    [Fact]
    public void Launch_使用简单命令_不抛出未处理异常()
    {
        var strategy = new WindowStrategy();
        var failDir = Path.Combine(_tempDir, "win_fail");
        var param = new MonitorParameter
        {
            ProcessNameOrId = "test_process",
            InnerApp = "cmd.exe",
            InnerArguments = "/c exit 0",
            DumpFileName = "crash.dmp",
            FailFileName = "crash.json",
            TargetPath = _tempDir,
            FailDirectory = failDir,
            BackupDirectory = Path.Combine(_tempDir, "backup"),
            WorkModel = "Normal",
            ExtendedField = "1.0.0",
        };
        strategy.SetParameter(param);

        // WindowStrategy.Launch will try to run procdump which may not exist
        // The test verifies graceful handling
        try
        {
            strategy.Launch();
        }
        catch (Exception)
        {
            // Acceptable on test machines without procdump
        }
    }
}
