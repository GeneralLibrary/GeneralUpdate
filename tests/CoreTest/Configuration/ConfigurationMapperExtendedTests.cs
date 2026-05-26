namespace CoreTest.Configuration;

using GeneralUpdate.Core.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="ConfigurationMapper"/> — additional edge case coverage beyond existing tests.
/// Covers: MapToProcessInfo required fields, MapToGlobalConfigInfo edge fields, CopyBaseFields cross-type.
/// </summary>
public class ConfigurationMapperExtendedTests : IDisposable
{
    private readonly string _tempInstallDir;

    public ConfigurationMapperExtendedTests()
    {
        _tempInstallDir = Path.Combine(Path.GetTempPath(), "MapToProcessInfo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempInstallDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempInstallDir)) Directory.Delete(_tempInstallDir, true); } catch { }
    }

    /// <summary>Creates a valid GlobalConfigInfo with all required fields for MapToProcessInfo.</summary>
    private GlobalConfigInfo CreateValidSource()
    {
        return new GlobalConfigInfo
        {
            MainAppName = "MainApp",
            InstallPath = _tempInstallDir,
            ClientVersion = "1.0.0",
            LastVersion = "2.0.0",
            Encoding = System.Text.Encoding.UTF8,
            Format = ".zip",
            AppSecretKey = "secret",
            ReportUrl = "https://report.example.com",
            BackupDirectory = Path.Combine(_tempInstallDir, "backup")
        };
    }

    private static List<VersionInfo> OneVersion(string ver = "2.0.0")
        => new() { new VersionInfo { Version = ver, Hash = "abc", Name = $"v{ver}.zip" } };

    #region MapToGlobalConfigInfo — additional edge cases

    [Fact]
    public void MapToGlobalConfigInfo_SourceWithBlackLists_CopiesCorrectly()
    {
        var source = new Configinfo
        {
            BlackFiles = new List<string> { "a.dll", "b.dll" },
            BlackFormats = new List<string> { ".log", ".tmp" },
            SkipDirectorys = new List<string> { "app-1.0", "cache" }
        };

        var result = ConfigurationMapper.MapToGlobalConfigInfo(source);

        Assert.Equal(2, result.BlackFiles?.Count);
        Assert.Equal(2, result.BlackFormats?.Count);
        Assert.Equal(2, result.SkipDirectorys?.Count);
    }

    [Fact]
    public void MapToGlobalConfigInfo_SourceWithNullLists_ReturnsNullLists()
    {
        var source = new Configinfo { BlackFiles = null, BlackFormats = null, SkipDirectorys = null };

        var result = ConfigurationMapper.MapToGlobalConfigInfo(source);

        Assert.Null(result.BlackFiles);
        Assert.Null(result.BlackFormats);
        Assert.Null(result.SkipDirectorys);
    }

    [Fact]
    public void MapToGlobalConfigInfo_PreservesExistingTargetFieldsNotInSource()
    {
        var target = new GlobalConfigInfo
        {
            TempPath = "/custom/temp",
            BackupDirectory = "/custom/backup",
            MaxConcurrency = 8
        };
        var source = new Configinfo { AppName = "NewApp.exe" };

        var result = ConfigurationMapper.MapToGlobalConfigInfo(source, target);

        Assert.Equal("/custom/temp", result.TempPath);
        Assert.Equal("/custom/backup", result.BackupDirectory);
        Assert.Equal(8, result.MaxConcurrency);
        Assert.Equal("NewApp.exe", result.AppName);
    }

    #endregion

    #region MapToProcessInfo — valid path scenarios

    [Fact]
    public void MapToProcessInfo_WithVersionList_CopiesVersions()
    {
        var source = CreateValidSource();
        var versions = new List<VersionInfo>
        {
            new() { Version = "2.0.0", Hash = "abc", Name = "v2.zip" },
            new() { Version = "3.0.0", Hash = "def", Name = "v3.zip" }
        };

        var result = ConfigurationMapper.MapToProcessInfo(source, versions,
            new List<string>(), new List<string>(), new List<string>());

        Assert.Equal(2, result.UpdateVersions.Count);
        Assert.Equal("2.0.0", result.UpdateVersions[0].Version);
        Assert.Equal("3.0.0", result.UpdateVersions[1].Version);
    }

    [Fact]
    public void MapToProcessInfo_WithBlackAndSkipPaths_ListsSet()
    {
        var source = CreateValidSource();
        var blackFiles = new List<string> { "b1.dll" };
        var blackFormats = new List<string> { ".log" };
        var skipDirs = new List<string> { "temp" };

        var result = ConfigurationMapper.MapToProcessInfo(source,
            OneVersion(), blackFiles, blackFormats, skipDirs);

        // All blacklist lists should be set and non-null
        Assert.NotNull(result.BlackFiles);
        Assert.NotNull(result.BlackFileFormats);
        Assert.NotNull(result.SkipDirectorys);
    }

    [Fact]
    public void MapToProcessInfo_WithDriverDirectory_Works()
    {
        var source = CreateValidSource();
        source.DriverDirectory = "C:\\Drivers\\Special";

        var result = ConfigurationMapper.MapToProcessInfo(source,
            OneVersion(), new List<string>(), new List<string>(), new List<string>());

        Assert.Equal("C:\\Drivers\\Special", result.DriverDirectory);
    }

    [Fact]
    public void MapToProcessInfo_StandardConfig_CheckAllMappedProperties()
    {
        var source = CreateValidSource();
        source.UpdateLogUrl = "https://logs.test.com";
        source.Encoding = System.Text.Encoding.ASCII;
        source.Format = ".tar";
        source.DownloadTimeOut = 120;

        var result = ConfigurationMapper.MapToProcessInfo(source,
            OneVersion(), new List<string>(), new List<string>(), new List<string>());

        Assert.Equal("MainApp", result.AppName); // MainAppName -> AppName
        Assert.Equal(source.InstallPath, result.InstallPath);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("2.0.0", result.LastVersion);
        Assert.Equal("secret", result.AppSecretKey);
        Assert.Equal("us-ascii", result.CompressEncoding);
        Assert.Equal(".tar", result.CompressFormat);
        Assert.Equal(120, result.DownloadTimeOut);
        Assert.Equal("https://logs.test.com", result.UpdateLogUrl);
        Assert.Equal("https://report.example.com", result.ReportUrl);
        Assert.NotNull(result.BackupDirectory);
    }

    #endregion

    #region CopyBaseFields — cross-type between BaseConfigInfo subtypes

    [Fact]
    public void CopyBaseFields_ConfiginfoToGlobalConfigInfo_Works()
    {
        var source = new Configinfo
        {
            AppName = "App.exe",
            MainAppName = "Main",
            InstallPath = "C:\\app",
            ClientVersion = "v1",
            AppSecretKey = "key1",
            Token = "tok",
            Bowl = "bowl.exe",
            DriverDirectory = "C:\\drv"
        };
        var target = new GlobalConfigInfo();

        ConfigurationMapper.CopyBaseFields(source, target);

        Assert.Equal("App.exe", target.AppName);
        Assert.Equal("Main", target.MainAppName);
        Assert.Equal("C:\\app", target.InstallPath);
        Assert.Equal("v1", target.ClientVersion);
        Assert.Equal("key1", target.AppSecretKey);
        Assert.Equal("tok", target.Token);
        Assert.Equal("bowl.exe", target.Bowl);
        Assert.Equal("C:\\drv", target.DriverDirectory);
    }

    [Fact]
    public void CopyBaseFields_GraphCopy_ConfiginfoToNewConfiginfo_Works()
    {
        var source = new Configinfo
        {
            AppName = "Source.exe",
            ClientVersion = "5.0.0",
            Scheme = "https"
        };
        var target = new Configinfo();

        ConfigurationMapper.CopyBaseFields(source, target);

        Assert.Equal("Source.exe", target.AppName);
        Assert.Equal("5.0.0", target.ClientVersion);
        Assert.Equal("https", target.Scheme);
    }

    [Fact]
    public void CopyBaseFields_NullSource_DoesNotThrow()
    {
        var target = new GlobalConfigInfo { AppName = "keep" };

        var ex = Record.Exception(() =>
            ConfigurationMapper.CopyBaseFields<Configinfo, GlobalConfigInfo>(null!, target));

        Assert.Null(ex);
        Assert.Equal("keep", target.AppName);
    }

    #endregion
}
