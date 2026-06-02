using GeneralUpdate.Bowl.Strategies;

/// <summary>
/// 分支覆盖点：
/// ProcessExitResult 结构体：
///   - 默认构造：ExitCode=0，OutputLines=null
///   - 使用 init 设置 ExitCode 和 OutputLines
///   - OutputLines 为空列表
///   - OutputLines 含多行数据
///   - ExitCode 为 0（正常退出）
///   - ExitCode 为负数（异常退出）
///   - ExitCode 为 int 极值
/// </summary>
public class ProcessExitResultTests
{
    [Fact]
    public void 默认构造_ExitCode为0_OutputLines为null()
    {
        var result = new ProcessExitResult();
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.OutputLines);
    }

    [Fact]
    public void 设置ExitCode为0_正常退出场景()
    {
        var result = new ProcessExitResult
        {
            ExitCode = 0,
            OutputLines = new List<string>(),
        };
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void 设置ExitCode为负1_异常退出场景()
    {
        var result = new ProcessExitResult
        {
            ExitCode = -1,
            OutputLines = new List<string> { "error" },
        };
        Assert.Equal(-1, result.ExitCode);
        Assert.Single(result.OutputLines);
    }

    [Fact]
    public void 设置OutputLines为空列表_属性正确()
    {
        var result = new ProcessExitResult
        {
            ExitCode = 1,
            OutputLines = new List<string>(),
        };
        Assert.NotNull(result.OutputLines);
        Assert.Empty(result.OutputLines);
    }

    [Fact]
    public void 设置OutputLines含多行_属性正确()
    {
        var lines = new List<string> { "[10:00:00] Started", "[10:00:01] Monitoring", "[10:00:05] Exited" };
        var result = new ProcessExitResult
        {
            ExitCode = 0,
            OutputLines = lines,
        };
        Assert.Equal(3, result.OutputLines.Count);
        Assert.Contains("[10:00:01] Monitoring", result.OutputLines);
    }

    [Fact]
    public void ExitCode为int最小值_边界测试()
    {
        var result = new ProcessExitResult
        {
            ExitCode = int.MinValue,
            OutputLines = Array.Empty<string>(),
        };
        Assert.Equal(int.MinValue, result.ExitCode);
    }

    [Fact]
    public void ExitCode为int最大值_边界测试()
    {
        var result = new ProcessExitResult
        {
            ExitCode = int.MaxValue,
            OutputLines = Array.Empty<string>(),
        };
        Assert.Equal(int.MaxValue, result.ExitCode);
    }
}
