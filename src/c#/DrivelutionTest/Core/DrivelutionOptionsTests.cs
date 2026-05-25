using GeneralUpdate.Drivelution.Abstractions.Configuration;

namespace DrivelutionTest.Core;

public class DrivelutionOptionsTests
{
    [Fact(DisplayName = "DrivelutionOptions_默认构造函数_所有属性默认值")]
    public void DefaultConstructor_AllPropertiesDefault()
    {
        var options = new DrivelutionOptions();
        Assert.Equal("./DriverBackups", options.DefaultBackupPath);
        Assert.Equal(3, options.DefaultRetryCount);
        Assert.Equal(5, options.DefaultRetryIntervalSeconds);
        Assert.Equal(300, options.DefaultTimeoutSeconds);
        Assert.False(options.DebugModeSkipSignature);
        Assert.False(options.DebugModeSkipHash);
        Assert.True(options.ForceTerminateOnPermissionFailure);
        Assert.True(options.AutoCleanupBackups);
        Assert.Equal(5, options.BackupsToKeep);
        Assert.False(options.UseExponentialBackoff);
        Assert.NotNull(options.TrustedCertificateThumbprints);
        Assert.Empty(options.TrustedCertificateThumbprints);
        Assert.NotNull(options.TrustedGpgKeys);
        Assert.Empty(options.TrustedGpgKeys);
    }

    [Theory(DisplayName = "DrivelutionOptions_Boolean属性")]
    [InlineData(true)]
    [InlineData(false)]
    public void UseExponentialBackoff(bool value)
    {
        var options = new DrivelutionOptions { UseExponentialBackoff = value };
        Assert.Equal(value, options.UseExponentialBackoff);
    }

    [Fact(DisplayName = "DrivelutionOptions_TrustedCertificateThumbprints_可添加")]
    public void TrustedCertificateThumbprints_CanAdd()
    {
        var options = new DrivelutionOptions();
        options.TrustedCertificateThumbprints.Add("AB:CD:EF:01");
        Assert.Single(options.TrustedCertificateThumbprints);
    }

    [Fact(DisplayName = "DrivelutionOptions_TrustedGpgKeys_可添加")]
    public void TrustedGpgKeys_CanAdd()
    {
        var options = new DrivelutionOptions();
        options.TrustedGpgKeys.Add("KEY_1");
        Assert.Single(options.TrustedGpgKeys);
    }

    [Theory(DisplayName = "DrivelutionOptions_BackupsToKeep_可设置")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public void BackupsToKeep_CanSet(int value)
    {
        var options = new DrivelutionOptions { BackupsToKeep = value };
        Assert.Equal(value, options.BackupsToKeep);
    }
}
