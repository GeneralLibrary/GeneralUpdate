using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class ConfiginfoTests
{
    #region Validate — Null / Whitespace / Invalid URL

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_UpdateUrlNullOrWhitespace_ThrowsArgumentException(string updateUrl)
    {
        var config = new Configinfo
        {
            UpdateUrl = updateUrl,
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Invalid UpdateUrl", ex.Message);
    }

    [Fact]
    public void Validate_UpdateUrlNotWellFormedUri_ThrowsArgumentException()
    {
        var config = new Configinfo
        {
            UpdateUrl = "not_a_valid_uri!!!",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData("https://api.example.com/update")]
    [InlineData("http://localhost:5000/api/version")]
    public void Validate_UpdateUrlValid_DoesNotThrowForUpdateUrl(string url)
    {
        var config = new Configinfo
        {
            UpdateUrl = url,
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_UpdateLogUrlNull_Allowed()
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            UpdateLogUrl = null,
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_UpdateLogUrlInvalid_ThrowsArgumentException()
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            UpdateLogUrl = "not_a_uri!!!",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AppNameNullOrWhitespace_ThrowsArgumentException(string appName)
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = appName,
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MainAppNameNullOrWhitespace_ThrowsArgumentException(string mainAppName)
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "TestApp",
            MainAppName = mainAppName,
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AppSecretKeyNullOrWhitespace_ThrowsArgumentException(string secretKey)
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = secretKey,
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ClientVersionNullOrWhitespace_ThrowsArgumentException(string clientVersion)
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = clientVersion,
            InstallPath = "C:\\app"
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InstallPathNullOrWhitespace_ThrowsArgumentException(string installPath)
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret",
            ClientVersion = "1.0.0",
            InstallPath = installPath
        };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_AllFieldsValid_NoExceptionThrown()
    {
        var config = new Configinfo
        {
            UpdateUrl = "https://api.example.com/update",
            UpdateLogUrl = "https://api.example.com/log",
            AppName = "TestApp",
            MainAppName = "MainApp",
            AppSecretKey = "secret123",
            ClientVersion = "1.0.0",
            InstallPath = "C:\\app"
        };
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    #endregion
}
