using System.Text;
using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class ProcessInfoTests
{
    private static string ExistingDir => Path.GetTempPath();
    private static List<VersionInfo> SingleVersion => new() { new VersionInfo { Version = "2.0.0" } };

    [Fact]
    public void Ctor_AppNameNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo(null, ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("appName", ex.Message);
    }

    [Fact]
    public void Ctor_InstallPathDoesNotExist_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ProcessInfo("app", "C:\\nonexistent_path_xyz", "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("path does not exist", ex.Message);
    }

    [Fact]
    public void Ctor_CurrentVersionNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo("app", ExistingDir, null, "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("currentVersion", ex.Message);
    }

    [Fact]
    public void Ctor_LastVersionNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", null, null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("lastVersion", ex.Message);
    }

    [Fact]
    public void Ctor_DownloadTimeOutNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", -1, "key",
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("greater than 0", ex.Message);
    }

    [Fact]
    public void Ctor_DownloadTimeOutZero_Allowed()
    {
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 0, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);
        Assert.Equal(0, info.DownloadTimeOut);
    }

    [Fact]
    public void Ctor_AppSecretKeyNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, null,
                SingleVersion, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("appSecretKey", ex.Message);
    }

    [Theory]
    [InlineData(true)] // null list
    [InlineData(false)] // empty list
    public void Ctor_UpdateVersionsNullOrEmpty_ThrowsArgumentException(bool nullList)
    {
        var versions = nullList ? null : new List<VersionInfo>();
        var ex = Assert.Throws<ArgumentException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                versions, "url", "backup", null, null, null, null, null, null, null));
        Assert.Contains("Collection", ex.Message);
    }

    [Fact]
    public void Ctor_ReportUrlNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, null, "backup", null, null, null, null, null, null, null));
        Assert.Contains("reportUrl", ex.Message);
    }

    [Fact]
    public void Ctor_BackupDirectoryNull_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
                Encoding.UTF8, "ZIP", 30, "key",
                SingleVersion, "url", null, null, null, null, null, null, null, null));
        Assert.Contains("backupDirectory", ex.Message);
    }

    [Fact]
    public void Ctor_AllParametersValid_AllPropertiesSet()
    {
        var info = new ProcessInfo(
            "MyApp", ExistingDir, "1.0.0", "2.0.0", "https://log.example.com",
            Encoding.UTF8, ".zip", 60, "secret-key",
            SingleVersion, "https://report.example.com", "C:\\backup",
            "BowlProcess", "https", "token-abc", "C:\\drivers",
            new List<string> { ".tmp" }, new List<string> { "skip.dll" }, new List<string> { "logs" });

        Assert.Equal("MyApp", info.AppName);
        Assert.Equal(ExistingDir, info.InstallPath);
        Assert.Equal("1.0.0", info.CurrentVersion);
        Assert.Equal("2.0.0", info.LastVersion);
        Assert.Equal("https://log.example.com", info.UpdateLogUrl);
        Assert.Equal("utf-8", info.CompressEncoding);
        Assert.Equal(".zip", info.CompressFormat);
        Assert.Equal(60, info.DownloadTimeOut);
        Assert.Equal("secret-key", info.AppSecretKey);
        Assert.Single(info.UpdateVersions);
        Assert.Equal("https://report.example.com", info.ReportUrl);
        Assert.Equal("C:\\backup", info.BackupDirectory);
        Assert.Equal("BowlProcess", info.Bowl);
        Assert.Equal("https", info.Scheme);
        Assert.Equal("token-abc", info.Token);
        Assert.Equal("C:\\drivers", info.DriverDirectory);
        Assert.Single(info.BlackFileFormats);
        Assert.Single(info.BlackFiles);
        Assert.Single(info.SkipDirectorys);
    }

    [Fact]
    public void Ctor_EncodingUTF8_CompressEncodingWebNameIsUtf8()
    {
        // Arrange & Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);

        // Assert
        Assert.Equal("utf-8", info.CompressEncoding);
    }

    [Fact]
    public void Ctor_EncodingASCII_CompressEncodingWebNameIsAscii()
    {
        // Arrange & Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.ASCII, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);

        // Assert
        Assert.Equal("us-ascii", info.CompressEncoding);
    }

    [Fact]
    public void Ctor_EncodingUnicode_CompressEncodingWebNameIsUtf16()
    {
        // Arrange & Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.Unicode, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);

        // Assert
        Assert.Equal("utf-16", info.CompressEncoding);
    }

    [Fact]
    public void Ctor_NullableOptionalParams_AllowedAsNull()
    {
        // Arrange & Act — bowl, scheme, token, driverDirectory, blackList params are all nullable
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            SingleVersion, "url", "backup",
            null, null, null, null, null, null, null);

        // Assert
        Assert.Null(info.Bowl);
        Assert.Null(info.Scheme);
        Assert.Null(info.Token);
        Assert.Null(info.DriverDirectory);
        Assert.Null(info.BlackFileFormats);
        Assert.Null(info.BlackFiles);
        Assert.Null(info.SkipDirectorys);
    }

    [Fact]
    public void Ctor_DefaultConstructor_AllPropertiesDefault()
    {
        // Arrange & Act
        var info = new ProcessInfo();

        // Assert — default constructor should produce empty/null state
        Assert.Null(info.AppName);
        Assert.Null(info.InstallPath);
        Assert.Null(info.CurrentVersion);
        Assert.Null(info.LastVersion);
        Assert.Equal(0, info.DownloadTimeOut);
    }

    [Fact]
    public void Ctor_MultipleVersions_AllStored()
    {
        // Arrange
        var versions = new List<VersionInfo>
        {
            new() { Version = "1.0.0" },
            new() { Version = "1.1.0" },
            new() { Version = "2.0.0" }
        };

        // Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            versions, "url", "backup", null, null, null, null, null, null, null);

        // Assert
        Assert.Equal(3, info.UpdateVersions.Count);
        Assert.Equal("1.0.0", info.UpdateVersions[0].Version);
        Assert.Equal("1.1.0", info.UpdateVersions[1].Version);
        Assert.Equal("2.0.0", info.UpdateVersions[2].Version);
    }

    [Fact]
    public void Ctor_UpdateLogUrlNull_Allowed()
    {
        // Arrange & Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);

        // Assert — UpdateLogUrl is explicitly allowed to be null
        Assert.Null(info.UpdateLogUrl);
    }

    [Fact]
    public void Ctor_AllBlacklistParamsPopulated_PreservedInOrder()
    {
        // Arrange
        var formats = new List<string> { ".log", ".tmp", ".cache" };
        var files = new List<string> { "secret.key", "config.ini" };
        var dirs = new List<string> { "logs", "temp", "backups" };

        // Act
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null,
            formats, files, dirs);

        // Assert
        Assert.Equal(3, info.BlackFileFormats!.Count);
        Assert.Contains(".log", info.BlackFileFormats);
        Assert.Equal(2, info.BlackFiles!.Count);
        Assert.Contains("secret.key", info.BlackFiles);
        Assert.Equal(3, info.SkipDirectorys!.Count);
        Assert.Contains("logs", info.SkipDirectorys);
    }

}
