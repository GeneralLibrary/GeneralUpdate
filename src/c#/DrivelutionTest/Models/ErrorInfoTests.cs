using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// ErrorInfo 测试
/// 分支覆盖点:
/// - 默认构造函数：所有属性默认值
/// - 属性赋值：Code, Type, Message, Details, StackTrace, Timestamp, CanRetry, SuggestedResolution
/// - 可空属性：StackTrace为null, InnerException为null
/// - 枚举值遍历：ErrorType所有成员
/// 触发条件：创建 ErrorInfo 实例并设置属性
/// 预期结果：属性值正确返回
/// </summary>
public class ErrorInfoTests
{
    [Fact(DisplayName = "ErrorInfo_默认构造函数_所有属性为默认值")]
    public void ErrorInfo_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var error = new ErrorInfo();

        Assert.Equal(string.Empty, error.Code);
        Assert.Equal(default(ErrorType), error.Type);
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.Details);
        Assert.Null(error.StackTrace);
        Assert.Null(error.InnerException);
        Assert.Equal(default, error.CanRetry);
        Assert.Equal(string.Empty, error.SuggestedResolution);
    }

    [Fact(DisplayName = "ErrorInfo_Timestamp_默认值为UtcNow附近")]
    public void ErrorInfo_Timestamp_DefaultIsNearUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var error = new ErrorInfo();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(error.Timestamp, before, after);
    }

    [Fact(DisplayName = "ErrorInfo_设置Code属性_值正确返回")]
    public void ErrorInfo_SetCode_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { Code = "ERR_TIMEOUT" };
        Assert.Equal("ERR_TIMEOUT", error.Code);
    }

    [Fact(DisplayName = "ErrorInfo_设置Type为PermissionDenied_值正确返回")]
    public void ErrorInfo_SetTypePermissionDenied_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { Type = ErrorType.PermissionDenied };
        Assert.Equal(ErrorType.PermissionDenied, error.Type);
    }

    [Theory(DisplayName = "ErrorInfo_Type枚举_所有值均可设置")]
    [InlineData(ErrorType.PermissionDenied)]
    [InlineData(ErrorType.SignatureValidationFailed)]
    [InlineData(ErrorType.HashValidationFailed)]
    [InlineData(ErrorType.CompatibilityValidationFailed)]
    [InlineData(ErrorType.FileNotFound)]
    [InlineData(ErrorType.FileCorrupted)]
    [InlineData(ErrorType.BackupFailed)]
    [InlineData(ErrorType.InstallationFailed)]
    [InlineData(ErrorType.RollbackFailed)]
    [InlineData(ErrorType.NetworkError)]
    [InlineData(ErrorType.Timeout)]
    [InlineData(ErrorType.UserCancelled)]
    [InlineData(ErrorType.SystemNotSupported)]
    [InlineData(ErrorType.Unknown)]
    public void ErrorInfo_Type_AllEnumValuesCanBeSet(ErrorType type)
    {
        var error = new ErrorInfo { Type = type };
        Assert.Equal(type, error.Type);
    }

    [Fact(DisplayName = "ErrorInfo_设置Message属性_值正确返回")]
    public void ErrorInfo_SetMessage_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { Message = "Operation timed out" };
        Assert.Equal("Operation timed out", error.Message);
    }

    [Fact(DisplayName = "ErrorInfo_设置Details属性_值正确返回")]
    public void ErrorInfo_SetDetails_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { Details = "Detailed error information" };
        Assert.Equal("Detailed error information", error.Details);
    }

    [Fact(DisplayName = "ErrorInfo_StackTrace为null时_不抛出异常")]
    public void ErrorInfo_StackTraceIsNull_DoesNotThrow()
    {
        var error = new ErrorInfo { StackTrace = null };
        Assert.Null(error.StackTrace);
    }

    [Fact(DisplayName = "ErrorInfo_设置StackTrace属性_值正确返回")]
    public void ErrorInfo_SetStackTrace_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { StackTrace = "at Test.Method()" };
        Assert.Equal("at Test.Method()", error.StackTrace);
    }

    [Fact(DisplayName = "ErrorInfo_InnerException为null时_不抛出异常")]
    public void ErrorInfo_InnerExceptionIsNull_DoesNotThrow()
    {
        var error = new ErrorInfo { InnerException = null };
        Assert.Null(error.InnerException);
    }

    [Fact(DisplayName = "ErrorInfo_设置InnerException属性_值正确返回")]
    public void ErrorInfo_SetInnerException_ReturnsCorrectValue()
    {
        var ex = new InvalidOperationException("test");
        var error = new ErrorInfo { InnerException = ex };
        Assert.Same(ex, error.InnerException);
    }

    [Theory(DisplayName = "ErrorInfo_CanRetry_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void ErrorInfo_CanRetry_BothValuesCanBeSet(bool canRetry)
    {
        var error = new ErrorInfo { CanRetry = canRetry };
        Assert.Equal(canRetry, error.CanRetry);
    }

    [Fact(DisplayName = "ErrorInfo_设置SuggestedResolution属性_值正确返回")]
    public void ErrorInfo_SetSuggestedResolution_ReturnsCorrectValue()
    {
        var error = new ErrorInfo { SuggestedResolution = "Restart the application" };
        Assert.Equal("Restart the application", error.SuggestedResolution);
    }

    [Fact(DisplayName = "ErrorInfo_空字符串Code_不抛出异常")]
    public void ErrorInfo_EmptyStringCode_DoesNotThrow()
    {
        var error = new ErrorInfo { Code = "" };
        Assert.Equal(string.Empty, error.Code);
    }

    [Fact(DisplayName = "ErrorInfo_空字符串Message_不抛出异常")]
    public void ErrorInfo_EmptyStringMessage_DoesNotThrow()
    {
        var error = new ErrorInfo { Message = "" };
        Assert.Equal(string.Empty, error.Message);
    }
}
