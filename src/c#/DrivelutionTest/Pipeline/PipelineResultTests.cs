using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Pipeline;

namespace DrivelutionTest.Pipeline;

/// <summary>
/// PipelineResult 测试
/// 分支覆盖点:
/// - Ok() 静态方法：Success=true, ErrorMessage=null, Exception=null
/// - Fail(string) 静态方法：Success=false, ErrorMessage已设置, Exception=null
/// - Fail(string, Exception) 静态方法：Success=false, 已设置 ErrorMessage 和 Exception
/// - 空字符串 ErrorMessage
/// - Exception 为 null
/// 触发条件：调用 Ok() / Fail()
/// 预期结果：属性正确反映成功/失败状态
/// </summary>
public class PipelineResultTests
{
    [Fact(DisplayName = "PipelineResult_Ok_返回Success为true")]
    public void PipelineResult_Ok_ReturnsSuccessTrue()
    {
        var result = PipelineResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact(DisplayName = "PipelineResult_Fail_仅含消息_返回Success为false")]
    public void PipelineResult_Fail_MessageOnly_ReturnsSuccessFalse()
    {
        var result = PipelineResult.Fail("Something went wrong");

        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact(DisplayName = "PipelineResult_Fail_含消息和异常_返回Success为false")]
    public void PipelineResult_Fail_MessageAndException_ReturnsSuccessFalse()
    {
        var ex = new InvalidOperationException("inner error");
        var result = PipelineResult.Fail("Failed", ex);

        Assert.False(result.Success);
        Assert.Equal("Failed", result.ErrorMessage);
        Assert.Same(ex, result.Exception);
    }

    [Fact(DisplayName = "PipelineResult_Fail_空字符串消息_不抛出异常")]
    public void PipelineResult_Fail_EmptyMessage_DoesNotThrow()
    {
        var result = PipelineResult.Fail("");

        Assert.False(result.Success);
        Assert.Equal("", result.ErrorMessage);
    }

    [Fact(DisplayName = "PipelineResult_Fail_消息为null_允许null")]
    public void PipelineResult_Fail_NullMessage_AllowsNull()
    {
        var result = PipelineResult.Fail(null!);

        Assert.False(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact(DisplayName = "PipelineResult_Fail_Exception为null_不会包装")]
    public void PipelineResult_Fail_NullException_DoesNotWrap()
    {
        var result = PipelineResult.Fail("Error", null);

        Assert.False(result.Success);
        Assert.Equal("Error", result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact(DisplayName = "PipelineResult_Ok_多次调用返回不同实例")]
    public void PipelineResult_Ok_MultipleCallsReturnDifferentInstances()
    {
        var r1 = PipelineResult.Ok();
        var r2 = PipelineResult.Ok();

        Assert.NotSame(r1, r2);
        Assert.True(r1.Success);
        Assert.True(r2.Success);
    }
}

/// <summary>
/// PipelineContext 测试
/// 分支覆盖点:
/// - 构造函数正确初始化 DriverInfo, Strategy, Result
/// - 对 null 参数抛出 ArgumentNullException
/// - Bag 字典可读写
/// - Bag 初始为空
/// 触发条件：创建 PipelineContext
/// 预期结果：构造正确，null 检测生效
/// </summary>
public class PipelineContextTests
{
    private static DriverInfo CreateDriver() => new()
    {
        Name = "TestDriver",
        Version = "1.0.0",
        FilePath = "/test/path"
    };

    private static UpdateStrategy CreateStrategy() => new()
    {
        RequireBackup = true,
        BackupPath = "/backups"
    };

    private static UpdateResult CreateResult() => new()
    {
        Status = UpdateStatus.NotStarted
    };

    [Fact(DisplayName = "PipelineContext_构造函数_正确初始化所有属性")]
    public void PipelineContext_Constructor_InitializesAllProperties()
    {
        var driver = CreateDriver();
        var strategy = CreateStrategy();
        var result = CreateResult();

        var context = new PipelineContext(driver, strategy, result);

        Assert.Same(driver, context.DriverInfo);
        Assert.Same(strategy, context.Strategy);
        Assert.Same(result, context.Result);
        Assert.NotNull(context.Bag);
        Assert.Empty(context.Bag);
    }

    [Fact(DisplayName = "PipelineContext_DriverInfo为null_抛出ArgumentNullException")]
    public void PipelineContext_DriverInfoNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipelineContext(null!, CreateStrategy(), CreateResult()));
    }

    [Fact(DisplayName = "PipelineContext_Strategy为null_抛出ArgumentNullException")]
    public void PipelineContext_StrategyNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipelineContext(CreateDriver(), null!, CreateResult()));
    }

    [Fact(DisplayName = "PipelineContext_Result为null_抛出ArgumentNullException")]
    public void PipelineContext_ResultNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipelineContext(CreateDriver(), CreateStrategy(), null!));
    }

    [Fact(DisplayName = "PipelineContext_Bag_可存储和检索值")]
    public void PipelineContext_Bag_CanStoreAndRetrieveValues()
    {
        var context = new PipelineContext(CreateDriver(), CreateStrategy(), CreateResult());

        context.Bag["key1"] = "value1";
        context.Bag["key2"] = 42;
        context.Bag["BackupPath"] = "/backups/driver";

        Assert.Equal(3, context.Bag.Count);
        Assert.Equal("value1", context.Bag["key1"]);
        Assert.Equal(42, context.Bag["key2"]);
        Assert.Equal("/backups/driver", context.Bag["BackupPath"]);
    }

    [Fact(DisplayName = "PipelineContext_Bag_可存储null值")]
    public void PipelineContext_Bag_CanStoreNullValues()
    {
        var context = new PipelineContext(CreateDriver(), CreateStrategy(), CreateResult());

        context.Bag["nullKey"] = null;

        Assert.True(context.Bag.ContainsKey("nullKey"));
        Assert.Null(context.Bag["nullKey"]);
    }

    [Fact(DisplayName = "PipelineContext_Bag_TryGetValue_获取不存在的键返回false")]
    public void PipelineContext_Bag_TryGetValue_MissingKey_ReturnsFalse()
    {
        var context = new PipelineContext(CreateDriver(), CreateStrategy(), CreateResult());

        var found = context.Bag.TryGetValue("nonexistent", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact(DisplayName = "PipelineContext_Bag_TryGetValue_获取存在的键返回true")]
    public void PipelineContext_Bag_TryGetValue_ExistingKey_ReturnsTrue()
    {
        var context = new PipelineContext(CreateDriver(), CreateStrategy(), CreateResult());
        context.Bag["exists"] = "hello";

        var found = context.Bag.TryGetValue("exists", out var value);

        Assert.True(found);
        Assert.Equal("hello", value);
    }
}
