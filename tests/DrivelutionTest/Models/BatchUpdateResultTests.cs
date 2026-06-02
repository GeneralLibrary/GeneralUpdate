using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// BatchUpdateResult 测试
/// 分支覆盖点:
/// - 默认构造函数：所有属性默认值
/// - AllSucceeded 布尔值
/// - SucceededCount/FailedCount 整数值
/// - Results 列表操作
/// - Duration TimeSpan
/// - ToString 格式
/// 触发条件：创建 BatchUpdateResult 并设置属性
/// 预期结果：属性正确，ToString 格式化正确
/// </summary>
public class BatchUpdateResultTests
{
    [Fact(DisplayName = "BatchUpdateResult_默认构造函数_所有属性为默认值")]
    public void BatchUpdateResult_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var result = new BatchUpdateResult();

        Assert.False(result.AllSucceeded);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.NotNull(result.Results);
        Assert.Empty(result.Results);
        Assert.Equal(default(TimeSpan), result.Duration);
    }

    [Fact(DisplayName = "BatchUpdateResult_ToString_返回正确格式")]
    public void BatchUpdateResult_ToString_ReturnsCorrectFormat()
    {
        var result = new BatchUpdateResult
        {
            SucceededCount = 3,
            FailedCount = 1,
            Duration = TimeSpan.FromSeconds(15.5)
        };

        var str = result.ToString();
        Assert.Contains("3 succeeded", str);
        Assert.Contains("1 failed", str);
        Assert.Contains("15.5s", str);
    }

    [Fact(DisplayName = "BatchUpdateResult_AllSucceeded为true_代表全部成功")]
    public void BatchUpdateResult_AllSucceededIsTrue_RepresentsAllSuccess()
    {
        var result = new BatchUpdateResult { AllSucceeded = true };
        Assert.True(result.AllSucceeded);
    }

    [Fact(DisplayName = "BatchUpdateResult_SucceededCount为0_FailedCount也为0")]
    public void BatchUpdateResult_SucceededCountZero_FailedCountZero()
    {
        var result = new BatchUpdateResult
        {
            SucceededCount = 0,
            FailedCount = 0
        };

        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact(DisplayName = "BatchUpdateResult_Duration可设置TimeSpan值")]
    public void BatchUpdateResult_Duration_CanSetTimeSpanValue()
    {
        var duration = TimeSpan.FromMinutes(2);
        var result = new BatchUpdateResult { Duration = duration };
        Assert.Equal(duration, result.Duration);
    }

    [Fact(DisplayName = "BatchUpdateResult_Duration为零_ToString可处理")]
    public void BatchUpdateResult_DurationIsZero_ToStringHandlesIt()
    {
        var result = new BatchUpdateResult
        {
            SucceededCount = 1,
            Duration = TimeSpan.Zero
        };

        var str = result.ToString();
        Assert.Contains("0.0s", str);
    }

    [Fact(DisplayName = "BatchUpdateResult_Results_可以添加DriverUpdateEntry")]
    public void BatchUpdateResult_Results_CanAddDriverUpdateEntries()
    {
        var result = new BatchUpdateResult();
        var driver = new DriverInfo { Name = "TestDriver" };
        var entry = new DriverUpdateEntry
        {
            DriverInfo = driver,
            Success = true,
            Result = new UpdateResult { Success = true }
        };

        result.Results.Add(entry);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal("TestDriver", result.Results[0].DriverInfo.Name);
    }
}

/// <summary>
/// DriverUpdateEntry 测试
/// 分支覆盖点:
/// - 默认构造函数属性默认值
/// - DriverInfo 属性
/// - Success 布尔值
/// - Result 为 null 和非 null
/// 触发条件：创建 DriverUpdateEntry 并设置属性
/// 预期结果：属性值正确
/// </summary>
public class DriverUpdateEntryTests
{
    [Fact(DisplayName = "DriverUpdateEntry_默认构造函数_Success为false")]
    public void DriverUpdateEntry_DefaultConstructor_SuccessIsFalse()
    {
        var entry = new DriverUpdateEntry();
        Assert.False(entry.Success);
        Assert.Null(entry.DriverInfo);
        Assert.Null(entry.Result);
    }

    [Fact(DisplayName = "DriverUpdateEntry_设置DriverInfo_返回正确引用")]
    public void DriverUpdateEntry_SetDriverInfo_ReturnsCorrectReference()
    {
        var driver = new DriverInfo { Name = "Driver1", Version = "1.0.0" };
        var entry = new DriverUpdateEntry { DriverInfo = driver };

        Assert.Same(driver, entry.DriverInfo);
        Assert.Equal("Driver1", entry.DriverInfo.Name);
    }

    [Fact(DisplayName = "DriverUpdateEntry_Success为true_Result可为null")]
    public void DriverUpdateEntry_SuccessIsTrue_ResultCanBeNull()
    {
        var entry = new DriverUpdateEntry
        {
            Success = true,
            Result = null
        };

        Assert.True(entry.Success);
        Assert.Null(entry.Result);
    }

    [Fact(DisplayName = "DriverUpdateEntry_Success为false_Result包含错误信息")]
    public void DriverUpdateEntry_SuccessIsFalse_ResultHasErrorInfo()
    {
        var updateResult = new UpdateResult
        {
            Success = false,
            Error = new ErrorInfo { Code = "ERR_INSTALL", Message = "Install failed" }
        };

        var entry = new DriverUpdateEntry
        {
            DriverInfo = new DriverInfo { Name = "DriverX" },
            Success = false,
            Result = updateResult
        };

        Assert.False(entry.Success);
        Assert.NotNull(entry.Result);
        Assert.NotNull(entry.Result.Error);
        Assert.Equal("ERR_INSTALL", entry.Result.Error.Code);
    }
}

/// <summary>
/// BatchMode 枚举测试
/// 分支覆盖点:
/// - Sequential 值为 0
/// - Parallel 值为 1
/// 触发条件：转换为 int
/// 预期结果：值符合枚举约定
/// </summary>
public class BatchModeTests
{
    [Fact(DisplayName = "BatchMode_Sequential为0值")]
    public void BatchMode_Sequential_IsZero()
    {
        Assert.Equal(0, (int)BatchMode.Sequential);
    }

    [Fact(DisplayName = "BatchMode_Parallel为1值")]
    public void BatchMode_Parallel_IsOne()
    {
        Assert.Equal(1, (int)BatchMode.Parallel);
    }
}
