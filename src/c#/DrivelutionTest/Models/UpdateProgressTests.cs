using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// UpdateProgress 测试
/// 分支覆盖点:
/// - 默认构造函数：所有属性默认值
/// - 属性设置：CurrentStatus, StepName, Percentage, Message, StepIndex, TotalSteps
/// - ToString方法：不同值组合
/// - 边界值：Percentage 0和100, StepIndex 0, TotalSteps 0
/// 触发条件：创建 UpdateProgress 实例
/// 预期结果：属性正确，ToString 正确格式化
/// </summary>
public class UpdateProgressTests
{
    [Fact(DisplayName = "UpdateProgress_默认构造函数_所有属性为默认值")]
    public void UpdateProgress_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var progress = new UpdateProgress();

        Assert.Equal(default(UpdateStatus), progress.CurrentStatus);
        Assert.Equal(string.Empty, progress.StepName);
        Assert.Equal(0, progress.Percentage);
        Assert.Equal(string.Empty, progress.Message);
        Assert.Equal(0, progress.StepIndex);
        Assert.Equal(0, progress.TotalSteps);
    }

    [Fact(DisplayName = "UpdateProgress_ToString_返回正确格式")]
    public void UpdateProgress_ToString_ReturnsCorrectFormat()
    {
        var progress = new UpdateProgress
        {
            CurrentStatus = UpdateStatus.Updating,
            StepName = "Install",
            Percentage = 50,
            Message = "Installing driver",
            StepIndex = 2,
            TotalSteps = 5
        };

        var str = progress.ToString();

        // Format: [50%] Install (3/5): Installing driver
        Assert.Contains("[50%]", str);
        Assert.Contains("Install", str);
        Assert.Contains("(3/5)", str);
        Assert.Contains("Installing driver", str);
    }

    [Fact(DisplayName = "UpdateProgress_Percentage为0时_ToString正常")]
    public void UpdateProgress_PercentageIsZero_ToStringWorks()
    {
        var progress = new UpdateProgress
        {
            Percentage = 0,
            StepName = "Start",
            StepIndex = 0,
            TotalSteps = 4
        };

        var str = progress.ToString();
        Assert.Contains("[0%]", str);
        Assert.Contains("(1/4)", str);
    }

    [Fact(DisplayName = "UpdateProgress_Percentage为100时_ToString正常")]
    public void UpdateProgress_PercentageIs100_ToStringWorks()
    {
        var progress = new UpdateProgress
        {
            Percentage = 100,
            StepName = "Complete",
            StepIndex = 3,
            TotalSteps = 4
        };

        var str = progress.ToString();
        Assert.Contains("[100%]", str);
        Assert.Contains("(4/4)", str);
    }

    [Fact(DisplayName = "UpdateProgress_空Message时_ToString正常")]
    public void UpdateProgress_EmptyMessage_ToStringWorks()
    {
        var progress = new UpdateProgress
        {
            StepName = "Validate",
            StepIndex = 0,
            TotalSteps = 3
        };

        var str = progress.ToString();
        Assert.Contains("Validate", str);
        Assert.Contains("(1/3)", str);
    }

    [Fact(DisplayName = "UpdateProgress_TotalSteps为0时_避免除以零")]
    public void UpdateProgress_TotalStepsIsZero_NoDivideByZero()
    {
        var progress = new UpdateProgress
        {
            StepName = "Test",
            StepIndex = 0,
            TotalSteps = 0,
            Percentage = 50
        };

        var str = progress.ToString();
        Assert.NotNull(str);
        Assert.Contains("(1/0)", str);
    }

    [Theory(DisplayName = "UpdateProgress_各种UpdateStatus状态均可设置")]
    [InlineData(UpdateStatus.NotStarted)]
    [InlineData(UpdateStatus.Validating)]
    [InlineData(UpdateStatus.BackingUp)]
    [InlineData(UpdateStatus.Updating)]
    [InlineData(UpdateStatus.Verifying)]
    [InlineData(UpdateStatus.Succeeded)]
    [InlineData(UpdateStatus.Failed)]
    [InlineData(UpdateStatus.RolledBack)]
    public void UpdateProgress_CurrentStatus_AllValuesCanBeSet(UpdateStatus status)
    {
        var progress = new UpdateProgress { CurrentStatus = status };
        Assert.Equal(status, progress.CurrentStatus);
    }
}
