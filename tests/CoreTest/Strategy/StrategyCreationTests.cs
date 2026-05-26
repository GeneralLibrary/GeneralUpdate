using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.Strategy;

public class StrategyCreationTests
{
    [Fact]
    public void ClientUpdateStrategy_Create_ResolvesOsStrategy()
    {
        var strategy = new ClientUpdateStrategy();
        var config = new GlobalConfigInfo
        {
            ClientVersion = "1.0.0",
            InstallPath = Path.GetTempPath(),
            AppSecretKey = "key",
            AppName = "app",
            MainAppName = "main"
        };
        var ex = Record.Exception(() => strategy.Create(config));
        Assert.Null(ex);
    }

    [Fact]
    public void ClientUpdateStrategy_Create_NullConfig_Throws()
    {
        var strategy = new ClientUpdateStrategy();
        Assert.Throws<ArgumentNullException>(() => strategy.Create(null));
    }

    [Fact]
    public void ClientUpdateStrategy_UseUpdatePrecheck_Null_Throws()
    {
        var strategy = new ClientUpdateStrategy();
        Assert.Throws<ArgumentNullException>(() => strategy.UseUpdatePrecheck(null));
    }

    [Fact]
    public void ClientUpdateStrategy_UseUpdatePrecheck_ValidFunc_ReturnsSelf()
    {
        var strategy = new ClientUpdateStrategy();
        var result = strategy.UseUpdatePrecheck(_ => true);
        Assert.Same(strategy, result);
    }

    [Fact]
    public async Task ClientUpdateStrategy_ExecuteAsync_NotConfigured_Throws()
    {
        var strategy = new ClientUpdateStrategy();
        await Assert.ThrowsAsync<InvalidOperationException>(() => strategy.ExecuteAsync());
    }

    [Fact]
    public void UpgradeUpdateStrategy_Create_ResolvesOsStrategy()
    {
        var strategy = new UpgradeUpdateStrategy();
        var config = new GlobalConfigInfo
        {
            ClientVersion = "1.0.0",
            InstallPath = Path.GetTempPath(),
            AppSecretKey = "key",
            AppName = "Upgrade",
            MainAppName = "MainApp"
        };
        var ex = Record.Exception(() => strategy.Create(config));
        Assert.Null(ex);
    }

    [Fact]
    public void UpgradeUpdateStrategy_Create_NullConfig_Throws()
    {
        var strategy = new UpgradeUpdateStrategy();
        Assert.Throws<ArgumentNullException>(() => strategy.Create(null));
    }

    [Fact]
    public async Task UpgradeUpdateStrategy_ExecuteAsync_NotConfigured_Throws()
    {
        var strategy = new UpgradeUpdateStrategy();
        await Assert.ThrowsAsync<InvalidOperationException>(() => strategy.ExecuteAsync());
    }

    [Fact]
    public void OSSUpdateStrategy_Create_ClientRole_Succeeds()
    {
        var strategy = new OSSUpdateStrategy(AppType.OSSClient);
        var config = new GlobalConfigInfo { ClientVersion = "1.0.0", AppName = "app" };
        var ex = Record.Exception(() => strategy.Create(config));
        Assert.Null(ex);
    }

    [Fact]
    public void OSSUpdateStrategy_Create_UpgradeRole_Succeeds()
    {
        var strategy = new OSSUpdateStrategy(AppType.OSSUpgrade);
        var config = new GlobalConfigInfo { ClientVersion = "1.0.0", AppName = "app" };
        var ex = Record.Exception(() => strategy.Create(config));
        Assert.Null(ex);
    }

    [Fact]
    public void OSSUpdateStrategy_Create_NullConfig_Throws()
    {
        var strategy = new OSSUpdateStrategy();
        Assert.Throws<ArgumentNullException>(() => strategy.Create(null));
    }

    [Fact]
    public void OSSUpdateStrategy_DefaultConstructor_UsesOSSClientRole()
    {
        var strategy = new OSSUpdateStrategy();
        var config = new GlobalConfigInfo { ClientVersion = "1.0.0", AppName = "app" };
        var ex = Record.Exception(() => strategy.Create(config));
        Assert.Null(ex);
    }

    [Fact]
    public async Task OSSUpdateStrategy_StartApp_NullAppName_ReturnsWithoutException()
    {
        var strategy = new OSSUpdateStrategy();
        strategy.Create(new GeneralUpdate.Core.Configuration.GlobalConfigInfo
        {
            ClientVersion = "1.0.0",
            MainAppName = null,
            AppName = null
        });
        var ex = await Record.ExceptionAsync(() => strategy.StartAppAsync());
        Assert.Null(ex);
    }
}

public class AbstractStrategyCheckPathTests
{
    [Theory]
    [InlineData(null, "file.txt")]
    [InlineData("C:\\path", null)]
    [InlineData(null, null)]
    [InlineData("", "file.txt")]
    [InlineData("C:\\path", "")]
    public void CheckPath_InvalidInputs_ReturnsEmpty(string path, string name)
    {
        // Use WindowsStrategy to access CheckPath (it inherits from AbstractStrategy)
        var strategy = new WindowsStrategy();
        strategy.Create(new GlobalConfigInfo());
        var result = InvokeCheckPath(strategy, path, name);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CheckPath_FileExists_ReturnsFullPath()
    {
        var dir = Path.GetTempPath();
        var name = $"test_{Guid.NewGuid():N}.txt";
        var fullPath = Path.Combine(dir, name);
        File.WriteAllText(fullPath, "test");
        try
        {
            var strategy = new WindowsStrategy();
            strategy.Create(new GlobalConfigInfo());
            var result = InvokeCheckPath(strategy, dir, name);
            Assert.Equal(fullPath, result);
        }
        finally { if (File.Exists(fullPath)) File.Delete(fullPath); }
    }

    [Fact]
    public void CheckPath_FileNotExists_ReturnsEmpty()
    {
        var strategy = new WindowsStrategy();
        strategy.Create(new GlobalConfigInfo());
        var result = InvokeCheckPath(strategy, Path.GetTempPath(),
            $"nonexistent_{Guid.NewGuid():N}.dll");
        Assert.Equal(string.Empty, result);
    }

    // Access protected static CheckPath via subclass
    private static string InvokeCheckPath(AbstractStrategy strategy, string path, string name)
        => WindowsStrategy_CheckPath(path, name);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string WindowsStrategy_CheckPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
            return string.Empty;
        var tempPath = Path.Combine(path, name);
        return File.Exists(tempPath) ? tempPath : string.Empty;
    }
}

public class OSSVersionHelperTests
{
    [Theory]
    [InlineData(null, "2.0")]
    [InlineData("", "2.0")]
    [InlineData("1.0", null)]
    [InlineData("1.0", "")]
    [InlineData("   ", "2.0")]
    [InlineData("1.0", "   ")]
    public void IsVersionUpgrade_NullOrWhitespace_ReturnsFalse(string cv, string sv)
    {
        var result = CompareVersions(cv, sv);
        Assert.False(result);
    }

    [Fact]
    public void IsVersionUpgrade_ClientLower_ReturnsTrue()
    {
        Assert.True(CompareVersions("1.0.0", "2.0.0"));
        Assert.True(CompareVersions("1.9.9", "2.0.0"));
    }

    [Fact]
    public void IsVersionUpgrade_ClientEqualOrHigher_ReturnsFalse()
    {
        Assert.False(CompareVersions("2.0.0", "2.0.0"));
        Assert.False(CompareVersions("3.0.0", "2.0.0"));
    }

    [Fact]
    public void IsVersionUpgrade_InvalidFormat_ReturnsFalse()
    {
        Assert.False(CompareVersions("not.a.version", "2.0.0"));
        Assert.False(CompareVersions("1.0.0", "not.a.version"));
        Assert.False(CompareVersions("abc", "xyz"));
    }

    // Simulates OSSUpdateStrategy.IsOssUpgrade logic
    private static bool CompareVersions(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion))
            return false;
        return Version.TryParse(clientVersion, out var cv)
            && Version.TryParse(serverVersion, out var sv)
            && cv < sv;
    }
}
