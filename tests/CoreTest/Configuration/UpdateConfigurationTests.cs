using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="UpdateConfiguration"/> — default property values.
/// Covers: all default property values defined in the abstract base class.
/// </summary>
public class UpdateConfigurationTests
{
    private class TestableConfig : UpdateConfiguration { }

    [Fact]
    public void Ctor_AppName_DefaultsToUpdateExe()
    {
        var config = new TestableConfig();
        Assert.Equal("Update.exe", config.UpdateAppName);
    }

    [Fact]
    public void Ctor_MainAppName_DefaultsToClient()
    {
        var config = new TestableConfig();
        Assert.Equal("Client", config.MainAppName);
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
    public void Ctor_ClientVersion_DefaultsToVersion()
    {
        var config = new TestableConfig();
        Assert.Equal("1.0.0.0", config.ClientVersion);
    }

    [Fact]
    public void Ctor_Files_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Files);
    }

    [Fact]
    public void Ctor_Formats_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Formats);
    }

    [Fact]
    public void Ctor_Directories_DefaultsToNull()
    {
        var config = new TestableConfig();
        Assert.Null(config.Directories);
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
            Files = new List<string> { "a.dll" },
            Formats = new List<string> { ".log" },
            Directories = new List<string> { "temp" },
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
        Assert.Single(config.Files);
        Assert.Single(config.Formats);
        Assert.Single(config.Directories);
        Assert.Equal("https://report.example.com", config.ReportUrl);
        Assert.Equal("Bowl.exe", config.Bowl);
        Assert.Equal("https", config.Scheme);
        Assert.Equal("bearer-token", config.Token);
        Assert.Equal("C:\\Drivers", config.DriverDirectory);
    }
}
