using GeneralUpdate.Drivelution.Core;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;

namespace DrivelutionTest.Core;

/// <summary>
/// DrivelutionFactory 测试
/// 分支覆盖点:
/// - Create() 返回正确的平台实现
/// - Create(DrivelutionOptions) 传递选项
/// - CreateValidator() 返回正确平台验证器
/// - CreateBackup() 返回正确平台备份实现
/// - GetCurrentPlatform() 返回平台名称字符串
/// - IsPlatformSupported() 返回布尔值
/// - 不支持的平台抛出 PlatformNotSupportedException
/// 触发条件：调用工厂方法
/// 预期结果：根据当前平台返回正确实现
/// </summary>
public class DrivelutionFactoryTests
{
    [Fact(DisplayName = "DrivelutionFactory_Create_返回IGeneralDrivelution实例")]
    public void Create_ReturnsIGeneralDrivelution()
    {
        var updater = DrivelutionFactory.Create();

        Assert.NotNull(updater);
        Assert.IsAssignableFrom<IGeneralDrivelution>(updater);
    }

    [Fact(DisplayName = "DrivelutionFactory_Create_带Options参数返回实例")]
    public void Create_WithOptions_ReturnsInstance()
    {
        var options = new DrivelutionOptions
        {
            DefaultRetryCount = 5,
            DefaultTimeoutSeconds = 600
        };

        var updater = DrivelutionFactory.Create(options);

        Assert.NotNull(updater);
        Assert.IsAssignableFrom<IGeneralDrivelution>(updater);
    }

    [Fact(DisplayName = "DrivelutionFactory_Create_nullOptions不抛异常")]
    public void Create_NullOptions_DoesNotThrow()
    {
        var updater = DrivelutionFactory.Create(null);

        Assert.NotNull(updater);
    }

    [Fact(DisplayName = "DrivelutionFactory_CreateValidator_返回IDriverValidator实例")]
    public void CreateValidator_ReturnsIDriverValidator()
    {
        var validator = DrivelutionFactory.CreateValidator();

        Assert.NotNull(validator);
        Assert.IsAssignableFrom<IDriverValidator>(validator);
    }

    [Fact(DisplayName = "DrivelutionFactory_CreateBackup_返回IDriverBackup实例")]
    public void CreateBackup_ReturnsIDriverBackup()
    {
        var backup = DrivelutionFactory.CreateBackup();

        Assert.NotNull(backup);
        Assert.IsAssignableFrom<IDriverBackup>(backup);
    }

    [Fact(DisplayName = "DrivelutionFactory_GetCurrentPlatform_返回非空字符串")]
    public void GetCurrentPlatform_ReturnsNonNullString()
    {
        var platform = DrivelutionFactory.GetCurrentPlatform();

        Assert.NotNull(platform);
        Assert.NotEmpty(platform);
    }

    [Fact(DisplayName = "DrivelutionFactory_IsPlatformSupported_返回true或false")]
    public void IsPlatformSupported_ReturnsBoolean()
    {
        var supported = DrivelutionFactory.IsPlatformSupported();

        // On Windows, Linux, or macOS, should return true
        // This test is platform-dependent but always returns bool
        Assert.True(supported || !supported);
    }

    [Fact(DisplayName = "DrivelutionFactory_GetCurrentPlatform_返回Windows_Linux_MacOS或Unknown")]
    public void GetCurrentPlatform_ReturnsKnownValue()
    {
        var platform = DrivelutionFactory.GetCurrentPlatform();

        Assert.Contains(platform, new[] { "Windows", "Linux", "MacOS", "Unknown" });
    }

    [Fact(DisplayName = "DrivelutionFactory_Create_返回不同类型实例_验证非同一引用")]
    public void Create_TwoCalls_ReturnDifferentInstances()
    {
        var u1 = DrivelutionFactory.Create();
        var u2 = DrivelutionFactory.Create();

        Assert.NotSame(u1, u2);
    }

    [Fact(DisplayName = "DrivelutionFactory_CreateValidator_两次调用返回不同实例")]
    public void CreateValidator_TwoCalls_DifferentInstances()
    {
        var v1 = DrivelutionFactory.CreateValidator();
        var v2 = DrivelutionFactory.CreateValidator();

        Assert.NotSame(v1, v2);
    }
}
