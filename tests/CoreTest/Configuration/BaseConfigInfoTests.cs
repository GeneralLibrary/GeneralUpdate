using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="BaseConfigInfo"/> — default property values.
/// Covers: all default property values defined in the abstract base class.
/// </summary>
public class BaseConfigInfoTests
{
    private class TestableConfig : BaseConfigInfo { }

    [Fact]
    public void Ctor_AppName_DefaultsToUpdateExe()
    {
        var config = new TestableConfig();
        Assert.Equal("Update.exe", config.UpdateAppName);
    }

    [Fact]
    public void Ctor_MainAppName_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.MainAppName);
    }

    [Fact]
    public void Ctor_InstallPath_DefaultsToBaseDirectory()
    {
        var config = new TestableConfig();
        var expected = AppDomain.CurrentDomain.BaseDirectory;
        Assert.Equal(expected, config.InstallPath);
    }

    [Fact]
    public void Ctor_UpdateLogUrl_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.UpdateLogUrl);
    }

    [Fact]
    public void Ctor_AppSecretKey_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.AppSecretKey);
    }

    [Fact]
    public void Ctor_ClientVersion_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.ClientVersion);
    }

    [Fact]
    public void Ctor_BlackFiles_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.BlackFiles);
    }

    [Fact]
    public void Ctor_BlackFormats_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.BlackFormats);
    }

    [Fact]
    public void Ctor_SkipDirectorys_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.SkipDirectorys);
    }

    [Fact]
    public void Ctor_ReportUrl_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.ReportUrl);
    }

    [Fact]
    public void Ctor_Bowl_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Bowl);
    }

    [Fact]
    public void Ctor_Scheme_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Scheme);
    }

    [Fact]
    public void Ctor_Token_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Token);
    }

    [Fact]
    public void Ctor_DriverDirectory_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.DriverDirectory);
    }

    [Fact]
    public void AllProperties_CanBeSetAndGet()
    {
        var config = new TestableConfig
        {
            UpdateAppName = "MyApp.exe",
            MainAppName = "MainApp",
            InstallPath = "C:\\MyApp",
            UpdateLogUrl = "https://logs.example.com",
            AppSecretKey = "secret-key",
            ClientVersion = "1.2.3",
            BlackFiles = new List<string> { "a.dll" },
            BlackFormats = new List<string> { ".log" },
            SkipDirectorys = new List<string> { "temp" },
            ReportUrl = "https://report.example.com",
            Bowl = "Bowl.exe",
            Scheme = "https",
            Token = "bearer-token",
            DriverDirectory = "C:\\Drivers"
        };

        Assert.Equal("MyApp.exe", config.UpdateAppName);
        Assert.Equal("MainApp", config.MainAppName);
        Assert.Equal("C:\\MyApp", config.InstallPath);
        Assert.Equal("https://logs.example.com", config.UpdateLogUrl);
        Assert.Equal("secret-key", config.AppSecretKey);
        Assert.Equal("1.2.3", config.ClientVersion);
        Assert.Single(config.BlackFiles);
        Assert.Single(config.BlackFormats);
        Assert.Single(config.SkipDirectorys);
        Assert.Equal("https://report.example.com", config.ReportUrl);
        Assert.Equal("Bowl.exe", config.Bowl);
        Assert.Equal("https", config.Scheme);
        Assert.Equal("bearer-token", config.Token);
        Assert.Equal("C:\\Drivers", config.DriverDirectory);
    }
}
