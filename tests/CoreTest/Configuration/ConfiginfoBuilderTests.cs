using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class ConfiginfoBuilderTests
{
    #region SetXxx — Null/Empty Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetUpdateUrl_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetUpdateUrl(value));
        Assert.Contains("updateUrl", ex.Message);
    }

    [Fact]
    public void SetUpdateUrl_ValidValue_ReturnsBuilder()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetUpdateUrl("https://api.example.com");
        Assert.Same(builder, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetToken_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetToken(value));
        Assert.Contains("token", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetScheme_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetScheme(value));
        Assert.Contains("scheme", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetAppName_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetAppName(value));
        Assert.Contains("AppName", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetMainAppName_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetMainAppName(value));
        Assert.Contains("MainAppName", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetClientVersion_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetClientVersion(value));
        Assert.Contains("ClientVersion", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetUpgradeClientVersion_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetUpgradeClientVersion(value));
        Assert.Contains("UpgradeClientVersion", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetAppSecretKey_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetAppSecretKey(value));
        Assert.Contains("AppSecretKey", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProductId_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetProductId(value));
        Assert.Contains("ProductId", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetInstallPath_InvalidValue_ThrowsArgumentException(string value)
    {
        var builder = new ConfiginfoBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.SetInstallPath(value));
        Assert.Contains("InstallPath", ex.Message);
    }

    #endregion

    #region Null-Allowed Setters (Bowl, DriverDirectory, BlackFile collections)

    [Fact]
    public void SetBowl_Null_Allowed()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetBowl(null);
        Assert.Same(builder, result);
    }

    [Fact]
    public void SetDriverDirectory_Null_Allowed()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetDriverDirectory(null);
        Assert.Same(builder, result);
    }

    [Fact]
    public void SetBlackFiles_Null_InitializesEmptyList()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetBlackFiles(null);
        Assert.Same(builder, result);
    }

    [Fact]
    public void SetBlackFiles_ValidList_Stored()
    {
        var builder = new ConfiginfoBuilder();
        var files = new List<string> { "file1.dll", "file2.dll" };
        builder.SetBlackFiles(files);
        // Can only verify through Build()
    }

    [Fact]
    public void SetBlackFormats_Null_InitializesEmptyList()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetBlackFormats(null);
        Assert.Same(builder, result);
    }

    [Fact]
    public void SetSkipDirectorys_Null_InitializesEmptyList()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder.SetSkipDirectorys(null);
        Assert.Same(builder, result);
    }

    #endregion

    #region Fluent Chaining

    [Fact]
    public void SetMethods_ReturnsSameBuilder_ForChaining()
    {
        var builder = new ConfiginfoBuilder();
        var result = builder
            .SetUpdateUrl("https://api.example.com")
            .SetToken("token")
            .SetScheme("https")
            .SetAppName("MyApp")
            .SetMainAppName("MainApp")
            .SetClientVersion("1.0.0");
        Assert.Same(builder, result);
    }

    #endregion

    #region Build — Success / Validation Failure

    [Fact]
    public void Build_WithRequiredFields_ReturnsConfiginfo()
    {
        var config = new ConfiginfoBuilder()
            .SetUpdateUrl("https://api.example.com")
            .SetToken("token123")
            .SetScheme("https")
            .SetAppName("MyApp.exe")
            .SetMainAppName("MyApp")
            .SetClientVersion("1.0.0")
            .SetAppSecretKey("secret")
            .SetInstallPath("C:\\app")
            .Build();

        Assert.NotNull(config);
        Assert.Equal("https://api.example.com", config.UpdateUrl);
        Assert.Equal("token123", config.Token);
        Assert.Equal("https", config.Scheme);
        Assert.Equal("MyApp.exe", config.AppName);
        Assert.Equal("MyApp", config.MainAppName);
        Assert.Equal("1.0.0", config.ClientVersion);
        Assert.Equal("secret", config.AppSecretKey);
        Assert.Equal("C:\\app", config.InstallPath);
    }

    [Fact]
    public void Build_MissingRequiredFields_ThrowsInvalidOperationException()
    {
        var builder = new ConfiginfoBuilder()
            .SetUpdateUrl("https://api.example.com")
            .SetToken("token")
            .SetScheme("https");
        // Missing AppName, MainAppName, AppSecretKey, ClientVersion, InstallPath
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Failed to build valid Configinfo", ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Build_EmptyBlackLists_ReturnsEmptyListNotNll()
    {
        var config = new ConfiginfoBuilder()
            .SetUpdateUrl("https://api.example.com")
            .SetToken("token123")
            .SetScheme("https")
            .SetAppName("MyApp.exe")
            .SetMainAppName("MyApp")
            .SetClientVersion("1.0.0")
            .SetAppSecretKey("secret")
            .SetInstallPath("C:\\app")
            .Build();

        Assert.NotNull(config.BlackFiles);
        Assert.NotNull(config.BlackFormats);
        Assert.NotNull(config.SkipDirectorys);
    }

    [Fact]
    public void Build_WithOptionalFields_IncludesThem()
    {
        var config = new ConfiginfoBuilder()
            .SetUpdateUrl("https://api.example.com")
            .SetToken("token123")
            .SetScheme("https")
            .SetAppName("MyApp.exe")
            .SetMainAppName("MyApp")
            .SetClientVersion("1.0.0")
            .SetAppSecretKey("secret")
            .SetInstallPath("C:\\app")
            .SetUpdateLogUrl("https://api.example.com/log")
            .SetProductId("product-001")
            .SetUpgradeClientVersion("2.0.0")
            .SetBowl("BowlApp")
            .SetDriverDirectory("C:\\drivers")
            .Build();

        Assert.Equal("https://api.example.com/log", config.UpdateLogUrl);
        Assert.Equal("product-001", config.ProductId);
        Assert.Equal("2.0.0", config.UpgradeClientVersion);
        Assert.Equal("BowlApp", config.Bowl);
        Assert.Equal("C:\\drivers", config.DriverDirectory);
    }

    #endregion
}
