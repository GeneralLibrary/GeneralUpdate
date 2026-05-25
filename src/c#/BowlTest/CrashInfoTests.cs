using GeneralUpdate.Bowl;

/// <summary>
/// 分支覆盖点：
/// CrashInfo 结构体：
///   - 默认构造：所有字段为默认值
///   - 使用 init 设置所有属性后读取
///   - DumpFilePath 为绝对路径
///   - CrashReportPath 为绝对路径
///   - Version 为空字符串
///   - ExitCode 为异常退出码（负数）
///   - ExitCode 为正常退出码（0）
/// </summary>
public class CrashInfoTests
{
    [Fact]
    public void 默认构造_所有属性为默认值()
    {
        var info = new CrashInfo();
        Assert.Null(info.DumpFilePath);
        Assert.Null(info.CrashReportPath);
        Assert.Null(info.Version);
        Assert.Equal(0, info.ExitCode);
    }

    [Fact]
    public void 使用init设置属性_所有属性正确返回()
    {
        var info = new CrashInfo
        {
            DumpFilePath = "C:\\fail\\v1\\v1_fail.dmp",
            CrashReportPath = "C:\\fail\\v1\\v1_fail.json",
            Version = "2.0.0",
            ExitCode = -1073741819, // ACCESS_VIOLATION
        };
        Assert.Equal("C:\\fail\\v1\\v1_fail.dmp", info.DumpFilePath);
        Assert.Equal("C:\\fail\\v1\\v1_fail.json", info.CrashReportPath);
        Assert.Equal("2.0.0", info.Version);
        Assert.Equal(-1073741819, info.ExitCode);
    }

    [Fact]
    public void Linux路径场景_属性正确()
    {
        var info = new CrashInfo
        {
            DumpFilePath = "/tmp/fail/1.0.0/1.0.0_fail.dmp",
            CrashReportPath = "/tmp/fail/1.0.0/1.0.0_fail.json",
            Version = "1.0.0",
            ExitCode = 139, // SIGSEGV
        };
        Assert.Equal("/tmp/fail/1.0.0/1.0.0_fail.dmp", info.DumpFilePath);
        Assert.Equal("/tmp/fail/1.0.0/1.0.0_fail.json", info.CrashReportPath);
        Assert.Equal("1.0.0", info.Version);
        Assert.Equal(139, info.ExitCode);
    }

    [Fact]
    public void 正常退出码_ExitCode为0()
    {
        var info = new CrashInfo
        {
            DumpFilePath = "/dump/0.dmp",
            CrashReportPath = "/dump/0.json",
            Version = "0.0.1",
            ExitCode = 0,
        };
        Assert.Equal(0, info.ExitCode);
    }
}
