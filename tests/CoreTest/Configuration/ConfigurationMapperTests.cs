using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class ConfigurationMapperTests
{
    [Fact]
    public void MapToGlobalConfigInfo_TargetNull_CreatesNewInstance()
    {
        var source = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "TestApp",
            MainAppName = "MainApp",
            ClientVersion = "1.0.0",
            AppSecretKey = "secret",
            InstallPath = "C:\\app"
        };
        var result = ConfigurationMapper.MapToGlobalConfigInfo(source, null);
        Assert.NotNull(result);
    }

    [Fact]
    public void MapToGlobalConfigInfo_SourceNull_ReturnsEmptyTarget()
    {
        var target = new GlobalConfigInfo { AppName = "existing" };
        var result = ConfigurationMapper.MapToGlobalConfigInfo(null, target);
        Assert.Same(target, result);
        Assert.Equal("existing", result.AppName); // Unchanged
    }

    [Fact]
    public void MapToGlobalConfigInfo_BothNull_ReturnsNewEmptyInstance()
    {
        var result = ConfigurationMapper.MapToGlobalConfigInfo(null, null);
        Assert.NotNull(result);
    }

    [Fact]
    public void MapToGlobalConfigInfo_MapsAllFields()
    {
        var source = new Configinfo
        {
            UpdateUrl = "https://api.example.com",
            AppName = "App.exe",
            MainAppName = "MainApp",
            ClientVersion = "2.0.0",
            AppSecretKey = "key123",
            InstallPath = "C:\\install",
            UpdateLogUrl = "https://log.example.com",
            ProductId = "prod-1",
            UpgradeClientVersion = "3.0.0",
            Token = "token123",
            Scheme = "https"
        };
        var result = ConfigurationMapper.MapToGlobalConfigInfo(source);
        Assert.Equal("https://api.example.com", result.UpdateUrl);
        Assert.Equal("App.exe", result.AppName);
        Assert.Equal("MainApp", result.MainAppName);
        Assert.Equal("2.0.0", result.ClientVersion);
        Assert.Equal("key123", result.AppSecretKey);
        Assert.Equal("C:\\install", result.InstallPath);
        Assert.Equal("https://log.example.com", result.UpdateLogUrl);
        Assert.Equal("prod-1", result.ProductId);
        Assert.Equal("3.0.0", result.UpgradeClientVersion);
        Assert.Equal("token123", result.Token);
        Assert.Equal("https", result.Scheme);
    }

    [Fact]
    public void MapToProcessInfo_SourceNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ConfigurationMapper.MapToProcessInfo(null,
                new List<VersionInfo>(),
                new List<string>(),
                new List<string>(),
                new List<string>()));
    }

    [Fact]
    public void MapToProcessInfo_MapsAppNameToMainAppName()
    {
        var source = new GlobalConfigInfo
        {
            MainAppName = "MyMainApp",
            InstallPath = Path.GetTempPath(),
            ClientVersion = "1.0.0",
            LastVersion = "2.0.0",
            AppSecretKey = "secret",
            Encoding = System.Text.Encoding.UTF8,
            Format = ".zip",
            DownloadTimeOut = 30,
            UpdateLogUrl = "https://log.example.com",
            ReportUrl = "https://report.example.com",
            BackupDirectory = "C:\\backup"
        };
        var versions = new List<VersionInfo> { new() { Version = "2.0.0" } };

        var result = ConfigurationMapper.MapToProcessInfo(source, versions,
            new List<string>(), new List<string>(), new List<string>());

        Assert.Equal("MyMainApp", result.AppName);
        Assert.Equal(Path.GetTempPath(), result.InstallPath);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("2.0.0", result.LastVersion);
        Assert.Equal("utf-8", result.CompressEncoding);
        Assert.Equal(".zip", result.CompressFormat);
        Assert.Equal(30, result.DownloadTimeOut);
    }

    [Fact]
    public void CopyBaseFields_BothNull_DoesNotThrow()
    {
        var exception = Record.Exception(() => ConfigurationMapper.CopyBaseFields<Configinfo, GlobalConfigInfo>(null, null));
        Assert.Null(exception);
    }

    [Fact]
    public void CopyBaseFields_SourceNull_DoesNotThrow()
    {
        var target = new GlobalConfigInfo();
        var exception = Record.Exception(() => ConfigurationMapper.CopyBaseFields<Configinfo, GlobalConfigInfo>(null, target));
        Assert.Null(exception);
    }

    [Fact]
    public void CopyBaseFields_TargetNull_DoesNotThrow()
    {
        var source = new Configinfo { AppName = "source" };
        var exception = Record.Exception(() => ConfigurationMapper.CopyBaseFields<Configinfo, GlobalConfigInfo>(source, null));
        Assert.Null(exception);
    }

    [Fact]
    public void CopyBaseFields_CopiesAllBaseProperties()
    {
        var source = new Configinfo
        {
            AppName = "App.exe",
            MainAppName = "Main",
            InstallPath = "C:\\path",
            UpdateLogUrl = "https://log",
            AppSecretKey = "key",
            ClientVersion = "1.0",
            Token = "tok",
            Scheme = "https",
            Bowl = "bowl",
            DriverDirectory = "C:\\drivers"
        };
        var target = new GlobalConfigInfo();

        ConfigurationMapper.CopyBaseFields(source, target);

        Assert.Equal("App.exe", target.AppName);
        Assert.Equal("Main", target.MainAppName);
        Assert.Equal("C:\\path", target.InstallPath);
        Assert.Equal("https://log", target.UpdateLogUrl);
        Assert.Equal("key", target.AppSecretKey);
        Assert.Equal("1.0", target.ClientVersion);
        Assert.Equal("tok", target.Token);
        Assert.Equal("https", target.Scheme);
    }
}
