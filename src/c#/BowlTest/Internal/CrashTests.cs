using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// Crash 类：
///   - 默认构造：Parameter 和 ProcdumpOutPutLines 为 null
///   - 设置 Parameter 为非 null MonitorParameter
///   - 设置 ProcdumpOutPutLines 为 List&lt;string&gt;（空列表）
///   - 设置 ProcdumpOutPutLines 为 List&lt;string&gt;（含数据）
///   - ProcdumpOutPutLines 为 null（未捕获输出）
/// </summary>
public class CrashTests
{
    [Fact]
    public void 默认构造_Parameter为null()
    {
        var crash = new Crash();
        Assert.Null(crash.Parameter);
    }

    [Fact]
    public void 默认构造_ProcdumpOutPutLines为null()
    {
        var crash = new Crash();
        Assert.Null(crash.ProcdumpOutPutLines);
    }

    [Fact]
    public void 设置Parameter_读取正确()
    {
        var param = new MonitorParameter
        {
            ProcessNameOrId = "test.exe",
            DumpFileName = "fail.dmp",
        };
        var crash = new Crash { Parameter = param };
        Assert.NotNull(crash.Parameter);
        Assert.Equal("test.exe", crash.Parameter.ProcessNameOrId);
        Assert.Equal("fail.dmp", crash.Parameter.DumpFileName);
    }

    [Fact]
    public void 设置ProcdumpOutPutLines_空列表_读取正确()
    {
        var crash = new Crash { ProcdumpOutPutLines = new List<string>() };
        Assert.NotNull(crash.ProcdumpOutPutLines);
        Assert.Empty(crash.ProcdumpOutPutLines);
    }

    [Fact]
    public void 设置ProcdumpOutPutLines_含数据_读取正确()
    {
        var lines = new List<string> { "[10:00:00] ProcDump started.", "[10:00:01] Process exited." };
        var crash = new Crash { ProcdumpOutPutLines = lines };
        Assert.NotNull(crash.ProcdumpOutPutLines);
        Assert.Equal(2, crash.ProcdumpOutPutLines.Count);
        Assert.Contains("[10:00:00] ProcDump started.", crash.ProcdumpOutPutLines);
        Assert.Contains("[10:00:01] Process exited.", crash.ProcdumpOutPutLines);
    }

    [Fact]
    public void 设置Parameter和ProcdumpOutPutLines_同时存在()
    {
        var param = new MonitorParameter { ProcessNameOrId = "myapp" };
        var lines = new List<string> { "line1", "line2" };
        var crash = new Crash
        {
            Parameter = param,
            ProcdumpOutPutLines = lines,
        };
        Assert.NotNull(crash.Parameter);
        Assert.NotNull(crash.ProcdumpOutPutLines);
    }
}
