using GeneralUpdate.Bowl;

/// <summary>
/// 分支覆盖点：
/// BowlResult 结构体：
///   - 默认构造：所有字段为默认值
///   - Ok 静态属性：Success=true，其余默认值
///   - 使用 init 设置所有属性
///   - DumpFilePath/CrashReportPath 为 null（正常退出场景）
///   - DumpFilePath/CrashReportPath 有值（崩溃场景）
/// </summary>
public class BowlResultTests
{
    [Fact]
    public void 默认构造_所有属性为默认值()
    {
        var result = new BowlResult();
        Assert.False(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.DumpCaptured);
        Assert.Null(result.DumpFilePath);
        Assert.Null(result.CrashReportPath);
        Assert.False(result.Restored);
    }

    [Fact]
    public void Ok静态属性_Success为true()
    {
        var result = BowlResult.Ok;
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.DumpCaptured);
        Assert.Null(result.DumpFilePath);
        Assert.Null(result.CrashReportPath);
        Assert.False(result.Restored);
    }

    [Fact]
    public void 正常退出场景_Success为true无崩溃信息()
    {
        var result = new BowlResult
        {
            Success = true,
            ExitCode = 0,
            DumpCaptured = false,
            DumpFilePath = null,
            CrashReportPath = null,
            Restored = false,
        };
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.DumpCaptured);
        Assert.Null(result.DumpFilePath);
        Assert.Null(result.CrashReportPath);
        Assert.False(result.Restored);
    }

    [Fact]
    public void 崩溃场景_包含完整崩溃信息()
    {
        var result = new BowlResult
        {
            Success = false,
            ExitCode = -1,
            DumpCaptured = true,
            DumpFilePath = "C:\\fail\\v1\\v1_fail.dmp",
            CrashReportPath = "C:\\fail\\v1\\v1_fail.json",
            Restored = true,
        };
        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.True(result.DumpCaptured);
        Assert.Equal("C:\\fail\\v1\\v1_fail.dmp", result.DumpFilePath);
        Assert.Equal("C:\\fail\\v1\\v1_fail.json", result.CrashReportPath);
        Assert.True(result.Restored);
    }

    [Fact]
    public void 崩溃但未恢复场景_Restored为false()
    {
        var result = new BowlResult
        {
            Success = false,
            ExitCode = -1073741819,
            DumpCaptured = true,
            DumpFilePath = "/tmp/crash.dmp",
            CrashReportPath = "/tmp/crash.json",
            Restored = false,
        };
        Assert.False(result.Success);
        Assert.False(result.Restored);
    }

    [Fact]
    public void 未生成崩溃报告_CrashReportPath为null()
    {
        var result = new BowlResult
        {
            Success = false,
            ExitCode = -1,
            DumpCaptured = true,
            DumpFilePath = "/tmp/crash.dmp",
            CrashReportPath = null,
            Restored = false,
        };
        Assert.Null(result.CrashReportPath);
    }
}
