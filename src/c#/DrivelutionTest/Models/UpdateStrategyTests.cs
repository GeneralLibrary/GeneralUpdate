using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// UpdateStrategy 测试
/// 分支覆盖点:
/// - 默认构造函数：所有属性默认值
/// - Mode 枚举：Full, Incremental
/// - ForceUpdate 布尔值
/// - RequireBackup 布尔值（默认true）
/// - BackupPath 字符串
/// - RetryCount 整数值（包括0, 负数, 极值）
/// - RetryIntervalSeconds 整数值
/// - Priority 整数值
/// - RestartMode 枚举：None, Prompt, Delayed, Immediate
/// - SkipSignatureValidation 布尔值（默认false）
/// - SkipHashValidation 布尔值（默认false）
/// - TimeoutSeconds 整数值（默认300）
/// 触发条件：创建 UpdateStrategy 并设置各属性
/// 预期结果：属性值正确
/// </summary>
public class UpdateStrategyTests
{
    [Fact(DisplayName = "UpdateStrategy_默认构造函数_所有属性为默认值")]
    public void UpdateStrategy_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var strategy = new UpdateStrategy();

        Assert.Equal(UpdateMode.Full, strategy.Mode);
        Assert.False(strategy.ForceUpdate);
        Assert.True(strategy.RequireBackup);
        Assert.Equal(string.Empty, strategy.BackupPath);
        Assert.Equal(3, strategy.RetryCount);
        Assert.Equal(5, strategy.RetryIntervalSeconds);
        Assert.Equal(0, strategy.Priority);
        Assert.Equal(RestartMode.Prompt, strategy.RestartMode);
        Assert.False(strategy.SkipSignatureValidation);
        Assert.False(strategy.SkipHashValidation);
        Assert.Equal(300, strategy.TimeoutSeconds);
    }

    [Theory(DisplayName = "UpdateStrategy_Mode_两种枚举值均可设置")]
    [InlineData(UpdateMode.Full)]
    [InlineData(UpdateMode.Incremental)]
    public void UpdateStrategy_Mode_BothEnumValuesCanBeSet(UpdateMode mode)
    {
        var strategy = new UpdateStrategy { Mode = mode };
        Assert.Equal(mode, strategy.Mode);
    }

    [Theory(DisplayName = "UpdateStrategy_ForceUpdate_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateStrategy_ForceUpdate_BothValuesCanBeSet(bool force)
    {
        var strategy = new UpdateStrategy { ForceUpdate = force };
        Assert.Equal(force, strategy.ForceUpdate);
    }

    [Theory(DisplayName = "UpdateStrategy_RequireBackup_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateStrategy_RequireBackup_BothValuesCanBeSet(bool requireBackup)
    {
        var strategy = new UpdateStrategy { RequireBackup = requireBackup };
        Assert.Equal(requireBackup, strategy.RequireBackup);
    }

    [Fact(DisplayName = "UpdateStrategy_BackupPath_空字符串默认值")]
    public void UpdateStrategy_BackupPath_DefaultIsEmptyString()
    {
        var strategy = new UpdateStrategy();
        Assert.Equal(string.Empty, strategy.BackupPath);
    }

    [Fact(DisplayName = "UpdateStrategy_BackupPath_可设置路径")]
    public void UpdateStrategy_BackupPath_CanSetPath()
    {
        var strategy = new UpdateStrategy { BackupPath = "C:\\backups" };
        Assert.Equal("C:\\backups", strategy.BackupPath);
    }

    [Theory(DisplayName = "UpdateStrategy_RetryCount_各种整数值均可设置")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(-1)]
    public void UpdateStrategy_RetryCount_VariousValuesCanBeSet(int count)
    {
        var strategy = new UpdateStrategy { RetryCount = count };
        Assert.Equal(count, strategy.RetryCount);
    }

    [Theory(DisplayName = "UpdateStrategy_RetryIntervalSeconds_各种值均可设置")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(60)]
    public void UpdateStrategy_RetryIntervalSeconds_VariousValuesCanBeSet(int seconds)
    {
        var strategy = new UpdateStrategy { RetryIntervalSeconds = seconds };
        Assert.Equal(seconds, strategy.RetryIntervalSeconds);
    }

    [Theory(DisplayName = "UpdateStrategy_Priority_各种值均可设置")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void UpdateStrategy_Priority_VariousValuesCanBeSet(int priority)
    {
        var strategy = new UpdateStrategy { Priority = priority };
        Assert.Equal(priority, strategy.Priority);
    }

    [Theory(DisplayName = "UpdateStrategy_RestartMode_所有枚举值均可设置")]
    [InlineData(RestartMode.None)]
    [InlineData(RestartMode.Prompt)]
    [InlineData(RestartMode.Delayed)]
    [InlineData(RestartMode.Immediate)]
    public void UpdateStrategy_RestartMode_AllEnumValuesCanBeSet(RestartMode mode)
    {
        var strategy = new UpdateStrategy { RestartMode = mode };
        Assert.Equal(mode, strategy.RestartMode);
    }

    [Theory(DisplayName = "UpdateStrategy_SkipSignatureValidation_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateStrategy_SkipSignatureValidation_BothValuesCanBeSet(bool skip)
    {
        var strategy = new UpdateStrategy { SkipSignatureValidation = skip };
        Assert.Equal(skip, strategy.SkipSignatureValidation);
    }

    [Theory(DisplayName = "UpdateStrategy_SkipHashValidation_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateStrategy_SkipHashValidation_BothValuesCanBeSet(bool skip)
    {
        var strategy = new UpdateStrategy { SkipHashValidation = skip };
        Assert.Equal(skip, strategy.SkipHashValidation);
    }

    [Theory(DisplayName = "UpdateStrategy_TimeoutSeconds_各种值均可设置")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(300)]
    [InlineData(3600)]
    [InlineData(int.MaxValue)]
    public void UpdateStrategy_TimeoutSeconds_VariousValuesCanBeSet(int timeout)
    {
        var strategy = new UpdateStrategy { TimeoutSeconds = timeout };
        Assert.Equal(timeout, strategy.TimeoutSeconds);
    }

    [Fact(DisplayName = "UpdateStrategy_TimeoutSeconds为0_表示无超时")]
    public void UpdateStrategy_TimeoutSecondsIsZero_RepresentsNoTimeout()
    {
        var strategy = new UpdateStrategy { TimeoutSeconds = 0 };
        Assert.Equal(0, strategy.TimeoutSeconds);
    }
}
