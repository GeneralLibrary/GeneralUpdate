using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// MonitorParameter 类（已标记 Obsolete）：
///   - 默认构造：所有属性为 null/default
///   - WorkModel 默认值为 "Upgrade"
///   - 设置所有公开属性
///   - 设置内部属性（InnerArguments, InnerApp）
///   - TargetPath 为路径字符串
///   - ProcessNameOrId 为进程名
///   - ExtendedField 为版本号
/// </summary>
public class MonitorParameterTests
{
    [Fact]
    public void 默认构造_WorkModel默认值为Upgrade()
    {
        var param = new MonitorParameter();
        Assert.Equal("Upgrade", param.WorkModel);
    }

    [Fact]
    public void 默认构造_其他属性为null()
    {
        var param = new MonitorParameter();
        Assert.Null(param.TargetPath);
        Assert.Null(param.FailDirectory);
        Assert.Null(param.BackupDirectory);
        Assert.Null(param.ProcessNameOrId);
        Assert.Null(param.DumpFileName);
        Assert.Null(param.FailFileName);
        Assert.Null(param.ExtendedField);
    }

    [Fact]
    public void 设置所有公开属性_正确返回()
    {
        var param = new MonitorParameter
        {
            TargetPath = "C:\\app",
            FailDirectory = "C:\\app\\fail",
            BackupDirectory = "C:\\app\\backup",
            ProcessNameOrId = "test.exe",
            DumpFileName = "v1_fail.dmp",
            FailFileName = "v1_fail.json",
            WorkModel = "Normal",
            ExtendedField = "1.0.0",
        };

        Assert.Equal("C:\\app", param.TargetPath);
        Assert.Equal("C:\\app\\fail", param.FailDirectory);
        Assert.Equal("C:\\app\\backup", param.BackupDirectory);
        Assert.Equal("test.exe", param.ProcessNameOrId);
        Assert.Equal("v1_fail.dmp", param.DumpFileName);
        Assert.Equal("v1_fail.json", param.FailFileName);
        Assert.Equal("Normal", param.WorkModel);
        Assert.Equal("1.0.0", param.ExtendedField);
    }

    [Fact]
    public void WorkModel为Upgrade_保留Upgrade()
    {
        var param = new MonitorParameter { WorkModel = "Upgrade" };
        Assert.Equal("Upgrade", param.WorkModel);
    }

    [Fact]
    public void WorkModel为null_允许null()
    {
        var param = new MonitorParameter { WorkModel = null! };
        Assert.Null(param.WorkModel);
    }

    [Fact]
    public void WorkModel为空字符串_允许空字符串()
    {
        var param = new MonitorParameter { WorkModel = string.Empty };
        Assert.Equal(string.Empty, param.WorkModel);
    }

    [Fact]
    public void ExtendedField为版本号_正确返回()
    {
        var param = new MonitorParameter { ExtendedField = "10.0.0-preview.1" };
        Assert.Equal("10.0.0-preview.1", param.ExtendedField);
    }
}
