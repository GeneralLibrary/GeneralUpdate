using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// AbstractStrategy：
///   - SetParameter(MonitorParameter)：设置 _parameter
///   - Launch()：调用 Startup()，Startup 执行完整启动流程
///   - Startup() 内部：
///     - FailDirectory 已存在 → 删除目录
///     - FailDirectory 不存在 → 仅创建
///     - Process.Start() → 进程启动
///     - OutputDataReceived / ErrorDataReceived → 添加非 null 非空行
///     - WaitForExit(10000) → 等待最多 10 秒
///   - OutputHandler()：
///     - Data 为 null → 不添加
///     - Data 为空字符串 → 不添加
///     - Data 有效 → 添加
/// </summary>
public class AbstractStrategyTests : IDisposable
{
    private class TestableAbstractStrategy : AbstractStrategy
    {
        public new MonitorParameter _parameter => base._parameter;
        public new List<string> OutputList => base.OutputList;
        public new void TestSetParameter(MonitorParameter p) => base.SetParameter(p);
        public new void TestLaunch() => base.Launch();
    }

    private readonly string _tempDir;

    public AbstractStrategyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_Abstract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SetParameter_设置参数_parameter字段更新()
    {
        var strategy = new TestableAbstractStrategy();
        var param = new MonitorParameter { ProcessNameOrId = "test.exe" };

        strategy.TestSetParameter(param);

        Assert.NotNull(strategy._parameter);
        Assert.Equal("test.exe", strategy._parameter.ProcessNameOrId);
    }

    [Fact]
    public void SetParameter_覆盖参数_字段更新为新参数()
    {
        var strategy = new TestableAbstractStrategy();
        var param1 = new MonitorParameter { ProcessNameOrId = "app1.exe" };
        var param2 = new MonitorParameter { ProcessNameOrId = "app2.exe" };

        strategy.TestSetParameter(param1);
        strategy.TestSetParameter(param2);

        Assert.Equal("app2.exe", strategy._parameter.ProcessNameOrId);
    }

    [Fact]
    public void SetParameter_设置null_允许null()
    {
        var strategy = new TestableAbstractStrategy();
        strategy.TestSetParameter(null!);
        Assert.Null(strategy._parameter);
    }

    [Fact]
    public void 初始化时OutputList为空()
    {
        var strategy = new TestableAbstractStrategy();
        Assert.NotNull(strategy.OutputList);
        Assert.Empty(strategy.OutputList);
    }

    [Fact]
    public void TestLaunch_SuccessWhenFailDirectoryCreated()
    {
        var strategy = new TestableAbstractStrategy();
        var failDir = Path.Combine(_tempDir, "fail");
        // Use a simple cmd.exe that exits quickly
        var param = new MonitorParameter
        {
            ProcessNameOrId = "test",
            InnerApp = "cmd.exe",
            InnerArguments = "/c exit 0",
            FailDirectory = failDir,
        };
        strategy.TestSetParameter(param);

        strategy.TestLaunch();

        // FailDirectory should exist after Launch
        Assert.True(Directory.Exists(failDir));
    }
}
