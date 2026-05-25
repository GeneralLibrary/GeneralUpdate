using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace DrivelutionTest.Exceptions;

/// <summary>
/// DrivelutionException 测试
/// 分支覆盖点:
/// - 双参数构造函数 (message, errorCode, canRetry)
/// - 三参数构造函数 (message, innerException, errorCode, canRetry)
/// - ErrorCode 属性默认值
/// - CanRetry 默认 false
/// - 极值：空message，null innerException
/// 触发条件：抛出和捕获异常
/// 预期结果：属性正确传播
/// </summary>
public class DrivelutionExceptionTests
{
    [Fact(DisplayName = "DrivelutionException_双参数构造函数_正确设置Message和ErrorCode")]
    public void DrivelutionException_TwoParamConstructor_SetsMessageAndErrorCode()
    {
        var ex = new DrivelutionException("Test error", "ERR_001");

        Assert.Equal("Test error", ex.Message);
        Assert.Equal("ERR_001", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DrivelutionException_双参数构造函数_默认ErrorCode为DR_UNKNOWN")]
    public void DrivelutionException_TwoParamConstructor_DefaultErrorCode()
    {
        var ex = new DrivelutionException("Test error");

        Assert.Equal("DR_UNKNOWN", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DrivelutionException_CanRetry为true_可设置")]
    public void DrivelutionException_CanRetryTrue_CanBeSet()
    {
        var ex = new DrivelutionException("Test", "ERR_RETRY", true);
        Assert.True(ex.CanRetry);
    }

    [Fact(DisplayName = "DrivelutionException_三参数构造函数_包含InnerException")]
    public void DrivelutionException_ThreeParamConstructor_IncludesInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DrivelutionException("outer", inner, "ERR_WRAP");

        Assert.Equal("outer", ex.Message);
        Assert.Equal("ERR_WRAP", ex.ErrorCode);
        Assert.Same(inner, ex.InnerException);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DrivelutionException_空消息_不会抛出额外异常")]
    public void DrivelutionException_EmptyMessage_DoesNotThrowExtra()
    {
        var ex = new DrivelutionException("");
        Assert.Equal("", ex.Message);
    }

    [Fact(DisplayName = "DrivelutionException_ErrorCode可动态修改")]
    public void DrivelutionException_ErrorCode_CanBeDynamicallySet()
    {
        var ex = new DrivelutionException("test") { ErrorCode = "NEW_CODE" };
        Assert.Equal("NEW_CODE", ex.ErrorCode);
    }

    [Fact(DisplayName = "DrivelutionException_CanRetry可动态修改")]
    public void DrivelutionException_CanRetry_CanBeDynamicallySet()
    {
        var ex = new DrivelutionException("test") { CanRetry = true };
        Assert.True(ex.CanRetry);
    }
}

/// <summary>
/// DriverPermissionException 测试
/// 分支覆盖点:
/// - 单参数构造函数 (message) => ErrorCode = "DR_PERMISSION_DENIED", CanRetry = false
/// - 双参数构造函数 (message, inner) => 同上
/// 触发条件：创建异常实例
/// 预期结果：ErrorCode 和 CanRetry 正确
/// </summary>
public class DriverPermissionExceptionTests
{
    [Fact(DisplayName = "DriverPermissionException_单参数构造函数_ErrorCode为DR_PERMISSION_DENIED")]
    public void DriverPermissionException_SingleParam_ErrorCodeCorrect()
    {
        var ex = new DriverPermissionException("Permission denied");

        Assert.Equal("Permission denied", ex.Message);
        Assert.Equal("DR_PERMISSION_DENIED", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverPermissionException_双参数构造函数_包含InnerException")]
    public void DriverPermissionException_TwoParam_IncludesInnerException()
    {
        var inner = new UnauthorizedAccessException("access denied");
        var ex = new DriverPermissionException("outer", inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("DR_PERMISSION_DENIED", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverPermissionException_是DrivelutionException的子类")]
    public void DriverPermissionException_IsSubclassOfDrivelutionException()
    {
        var ex = new DriverPermissionException("test");
        Assert.IsAssignableFrom<DrivelutionException>(ex);
    }
}

/// <summary>
/// DriverValidationException 测试
/// 分支覆盖点:
/// - 双参数构造函数 (message, validationType) => ErrorCode = "DR_VALIDATION_FAILED"
/// - 三参数构造函数 (message, validationType, inner) => 同上
/// - ValidationType 属性
/// 触发条件：创建异常实例
/// 预期结果：属性正确
/// </summary>
public class DriverValidationExceptionTests
{
    [Fact(DisplayName = "DriverValidationException_双参数构造函数_正确设置ValidationType")]
    public void DriverValidationException_TwoParam_SetsValidationType()
    {
        var ex = new DriverValidationException("Hash mismatch", "Integrity");

        Assert.Equal("Hash mismatch", ex.Message);
        Assert.Equal("Integrity", ex.ValidationType);
        Assert.Equal("DR_VALIDATION_FAILED", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverValidationException_三参数构造函数_包含InnerException")]
    public void DriverValidationException_ThreeParam_IncludesInnerException()
    {
        var inner = new InvalidOperationException("inner error");
        var ex = new DriverValidationException("outer", "Signature", inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("Signature", ex.ValidationType);
    }

    [Theory(DisplayName = "DriverValidationException_ValidationType_各种类型均可设置")]
    [InlineData("Integrity")]
    [InlineData("Signature")]
    [InlineData("Compatibility")]
    [InlineData("")]
    public void DriverValidationException_ValidationType_VariousValues(string validationType)
    {
        var ex = new DriverValidationException("test", validationType);
        Assert.Equal(validationType, ex.ValidationType);
    }
}

/// <summary>
/// DriverInstallationException 测试
/// 分支覆盖点:
/// - 单参数构造函数 (message) => ErrorCode = "DR_INSTALLATION_FAILED", CanRetry = true（默认）
/// - 双参数构造函数 (message, canRetry) => 同上
/// - 三参数构造函数 (message, inner, canRetry) => 同上
/// - CanRetry 可设为 false
/// 触发条件：创建异常实例
/// 预期结果：CanRetry 默认 true，可覆盖
/// </summary>
public class DriverInstallationExceptionTests
{
    [Fact(DisplayName = "DriverInstallationException_单参数构造函数_CanRetry默认为true")]
    public void DriverInstallationException_SingleParam_CanRetryDefaultTrue()
    {
        var ex = new DriverInstallationException("Install failed");

        Assert.Equal("DR_INSTALLATION_FAILED", ex.ErrorCode);
        Assert.True(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverInstallationException_双参数_CanRetry设为false")]
    public void DriverInstallationException_TwoParam_CanRetryFalse()
    {
        var ex = new DriverInstallationException("Fatal install error", false);

        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverInstallationException_三参数_包含InnerException")]
    public void DriverInstallationException_ThreeParam_IncludesInner()
    {
        var inner = new Exception("inner");
        var ex = new DriverInstallationException("outer", inner, true);

        Assert.Same(inner, ex.InnerException);
        Assert.True(ex.CanRetry);
    }

    [Theory(DisplayName = "DriverInstallationException_CanRetry_两种值均可设置")]
    [InlineData(true)]
    [InlineData(false)]
    public void DriverInstallationException_CanRetry_BothValuesCanBeSet(bool canRetry)
    {
        var ex = new DriverInstallationException("test", canRetry);
        Assert.Equal(canRetry, ex.CanRetry);
    }
}

/// <summary>
/// DriverBackupException 测试
/// 分支覆盖点:
/// - 单参数构造函数 => ErrorCode = "DR_BACKUP_FAILED", CanRetry = true
/// - 双参数构造函数 => 同上
/// 触发条件：创建异常实例
/// 预期结果：CanRetry 默认 true
/// </summary>
public class DriverBackupExceptionTests
{
    [Fact(DisplayName = "DriverBackupException_单参数_ErrorCode和CanRetry正确")]
    public void DriverBackupException_SingleParam_CorrectDefaults()
    {
        var ex = new DriverBackupException("Backup failed");

        Assert.Equal("DR_BACKUP_FAILED", ex.ErrorCode);
        Assert.True(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverBackupException_双参数_包含InnerException")]
    public void DriverBackupException_TwoParam_IncludesInner()
    {
        var inner = new IOException("disk full");
        var ex = new DriverBackupException("Backup failed", inner);

        Assert.Same(inner, ex.InnerException);
        Assert.True(ex.CanRetry);
    }
}

/// <summary>
/// DriverRollbackException 测试
/// 分支覆盖点:
/// - 单参数构造函数 => ErrorCode = "DR_ROLLBACK_FAILED", CanRetry = false
/// - 双参数构造函数 => 同上
/// 触发条件：创建异常实例
/// 预期结果：CanRetry 默认 false
/// </summary>
public class DriverRollbackExceptionTests
{
    [Fact(DisplayName = "DriverRollbackException_单参数_ErrorCode和CanRetry正确")]
    public void DriverRollbackException_SingleParam_CorrectDefaults()
    {
        var ex = new DriverRollbackException("Rollback failed");

        Assert.Equal("DR_ROLLBACK_FAILED", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverRollbackException_双参数_包含InnerException")]
    public void DriverRollbackException_TwoParam_IncludesInner()
    {
        var inner = new IOException("file locked");
        var ex = new DriverRollbackException("Rollback failed", inner);

        Assert.Same(inner, ex.InnerException);
    }
}

/// <summary>
/// DriverCompatibilityException 测试
/// 分支覆盖点:
/// - 单参数构造函数 => ErrorCode = "DR_COMPATIBILITY_FAILED", CanRetry = false
/// - 双参数构造函数 => 同上
/// 触发条件：创建异常实例
/// 预期结果：属性正确
/// </summary>
public class DriverCompatibilityExceptionTests
{
    [Fact(DisplayName = "DriverCompatibilityException_单参数_ErrorCode正确")]
    public void DriverCompatibilityException_SingleParam_CorrectErrorCode()
    {
        var ex = new DriverCompatibilityException("Not compatible");

        Assert.Equal("DR_COMPATIBILITY_FAILED", ex.ErrorCode);
        Assert.False(ex.CanRetry);
    }

    [Fact(DisplayName = "DriverCompatibilityException_双参数_包含InnerException")]
    public void DriverCompatibilityException_TwoParam_IncludesInner()
    {
        var inner = new PlatformNotSupportedException("unsupported");
        var ex = new DriverCompatibilityException("outer", inner);

        Assert.Same(inner, ex.InnerException);
    }
}
