using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// UpdateResult 测试
/// 分支覆盖点:
/// - 默认构造函数属性默认值
/// - DurationMs 计算：EndTime > StartTime 返回正值，EndTime == StartTime 返回0
/// - StepLogs 列表为空，可添加
/// - Error 为 null 和 非 null
/// - BackupPath 为 null
/// - RolledBack 默认为 false
/// - Status 枚举所有值
/// 触发条件：创建 UpdateResult 并设置属性
/// 预期结果：属性正确返回
/// </summary>
public class UpdateResultTests
{
    [Fact(DisplayName = "UpdateResult_默认构造函数_所有属性为默认值")]
    public void UpdateResult_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var result = new UpdateResult();

        Assert.False(result.Success);
        Assert.Equal(default(UpdateStatus), result.Status);
        Assert.Null(result.Error);
        Assert.Equal(default, result.StartTime);
        Assert.Equal(default, result.EndTime);
        Assert.Null(result.BackupPath);
        Assert.False(result.RolledBack);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.StepLogs);
        Assert.Empty(result.StepLogs);
    }

    [Fact(DisplayName = "UpdateResult_DurationMs_EndTime大于StartTime_返回正毫秒数")]
    public void UpdateResult_DurationMs_EndTimeAfterStartTime_ReturnsPositiveMs()
    {
        var result = new UpdateResult
        {
            StartTime = new DateTime(2025, 1, 1, 12, 0, 0),
            EndTime = new DateTime(2025, 1, 1, 12, 0, 5)
        };

        Assert.Equal(5000, result.DurationMs);
    }

    [Fact(DisplayName = "UpdateResult_DurationMs_EndTime等于StartTime_返回0")]
    public void UpdateResult_DurationMs_EndTimeEqualsStartTime_ReturnsZero()
    {
        var time = DateTime.UtcNow;
        var result = new UpdateResult
        {
            StartTime = time,
            EndTime = time
        };

        Assert.Equal(0, result.DurationMs);
    }

    [Fact(DisplayName = "UpdateResult_Success为true_值正确返回")]
    public void UpdateResult_SuccessIsTrue_ReturnsTrue()
    {
        var result = new UpdateResult { Success = true };
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "UpdateResult_Status为Succeeded_值正确返回")]
    public void UpdateResult_StatusIsSucceeded_ReturnsSucceeded()
    {
        var result = new UpdateResult { Status = UpdateStatus.Succeeded };
        Assert.Equal(UpdateStatus.Succeeded, result.Status);
    }

    [Fact(DisplayName = "UpdateResult_Error不为null_值正确返回")]
    public void UpdateResult_ErrorNotNull_ReturnsErrorInfo()
    {
        var error = new ErrorInfo { Code = "ERR_TEST" };
        var result = new UpdateResult { Error = error };

        Assert.NotNull(result.Error);
        Assert.Equal("ERR_TEST", result.Error.Code);
    }

    [Fact(DisplayName = "UpdateResult_Error为null_不抛出异常")]
    public void UpdateResult_ErrorIsNull_DoesNotThrow()
    {
        var result = new UpdateResult { Error = null };
        Assert.Null(result.Error);
    }

    [Fact(DisplayName = "UpdateResult_BackupPath为null_不抛出异常")]
    public void UpdateResult_BackupPathIsNull_DoesNotThrow()
    {
        var result = new UpdateResult { BackupPath = null };
        Assert.Null(result.BackupPath);
    }

    [Fact(DisplayName = "UpdateResult_BackupPath有值_返回正确路径")]
    public void UpdateResult_BackupPathHasValue_ReturnsCorrectPath()
    {
        var result = new UpdateResult { BackupPath = "C:\\backups\\driver" };
        Assert.Equal("C:\\backups\\driver", result.BackupPath);
    }

    [Fact(DisplayName = "UpdateResult_RolledBack为true_值正确返回")]
    public void UpdateResult_RolledBackIsTrue_ReturnsTrue()
    {
        var result = new UpdateResult { RolledBack = true };
        Assert.True(result.RolledBack);
    }

    [Fact(DisplayName = "UpdateResult_StepLogs_可以添加日志条目")]
    public void UpdateResult_StepLogs_CanAddLogEntries()
    {
        var result = new UpdateResult();
        result.StepLogs.Add("[12:00:00] Step 1 completed");
        result.StepLogs.Add("[12:00:05] Step 2 completed");

        Assert.Equal(2, result.StepLogs.Count);
        Assert.Contains("[12:00:00] Step 1 completed", result.StepLogs);
    }

    [Fact(DisplayName = "UpdateResult_Message_可设置信息消息")]
    public void UpdateResult_Message_CanSetInfoMessage()
    {
        var result = new UpdateResult { Message = "Update completed successfully" };
        Assert.Equal("Update completed successfully", result.Message);
    }

    [Theory(DisplayName = "UpdateResult_Status_所有枚举值均可设置")]
    [InlineData(UpdateStatus.NotStarted)]
    [InlineData(UpdateStatus.Validating)]
    [InlineData(UpdateStatus.BackingUp)]
    [InlineData(UpdateStatus.Updating)]
    [InlineData(UpdateStatus.Verifying)]
    [InlineData(UpdateStatus.Succeeded)]
    [InlineData(UpdateStatus.Failed)]
    [InlineData(UpdateStatus.RolledBack)]
    public void UpdateResult_Status_AllEnumValuesCanBeSet(UpdateStatus status)
    {
        var result = new UpdateResult { Status = status };
        Assert.Equal(status, result.Status);
    }
}
