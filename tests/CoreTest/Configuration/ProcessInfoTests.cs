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
        var info = new ProcessInfo("app", ExistingDir, "1.0", "2.0", null,
            Encoding.UTF8, "ZIP", 30, "key",
            SingleVersion, "url", "backup", null, null, null, null, null, null, null);
        Assert.Equal("utf-8", info.CompressEncoding);
    }
}
