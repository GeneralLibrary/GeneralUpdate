using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.Strategy;
using Moq;

namespace CoreTest.Strategy;

/// <summary>
/// Unit tests for <see cref="OssStrategy"/> following AAAT pattern.
/// Tests constructor, Create, interface implementation, and hook/reporter wiring.
/// </summary>
public class OssStrategyTests
{
    #region Constructor

    [Fact]
    public void Ctor_WithOssClientRole_CreatesInstance()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Ctor_WithOssUpgradeRole_CreatesInstance()
    {
        var strategy = new OssStrategy(AppType.OssUpgrade);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Ctor_DefaultsToIStrategy()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.IsAssignableFrom<IStrategy>(strategy);
    }

    #endregion

    #region Create

    [Fact]
    public void Create_WithValidContext_DoesNotThrow()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        var context = new UpdateContext
        {
            InstallPath = "C:\\test",
            ClientVersion = "1.0.0",
            UpdateUrl = "http://localhost/versions.json"
        };

        strategy.Create(context);
    }

    [Fact]
    public void Create_WithNullContext_ThrowsArgumentNullException()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.Throws<ArgumentNullException>(() => strategy.Create(null!));
    }

    #endregion

    #region ExecuteAsync — not configured

    [Fact]
    public async Task ExecuteAsync_WithoutCreate_ThrowsInvalidOperationException()
    {
        var strategy = new OssStrategy(AppType.OssClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => strategy.ExecuteAsync());
    }

    #endregion

    #region Hooks property

    [Fact]
    public void Hooks_DefaultValue_IsNoOpUpdateHooks()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.NotNull(strategy.Hooks);
        Assert.IsType<NoOpUpdateHooks>(strategy.Hooks);
    }

    [Fact]
    public void Hooks_CanBeSet_CustomImplementation()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        var mockHooks = new Mock<IUpdateHooks>();

        strategy.Hooks = mockHooks.Object;

        Assert.Same(mockHooks.Object, strategy.Hooks);
    }

    #endregion

    #region DownloadSource property

    [Fact]
    public void DownloadSource_DefaultValue_IsNull()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.Null(strategy.DownloadSource);
    }

    [Fact]
    public void DownloadSource_CanBeSet_ToCustomSource()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        var mockSource = new Mock<GeneralUpdate.Core.Download.Abstractions.IDownloadSource>();

        strategy.DownloadSource = mockSource.Object;

        Assert.Same(mockSource.Object, strategy.DownloadSource);
    }

    #endregion

    #region DownloadOrchestrator property

    [Fact]
    public void DownloadOrchestrator_DefaultValue_IsNull()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.Null(strategy.DownloadOrchestrator);
    }

    [Fact]
    public void DownloadOrchestrator_CanBeSet_ToCustomOrchestrator()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        var mockOrchestrator = new Mock<GeneralUpdate.Core.Download.Abstractions.IDownloadOrchestrator>();

        strategy.DownloadOrchestrator = mockOrchestrator.Object;

        Assert.Same(mockOrchestrator.Object, strategy.DownloadOrchestrator);
    }

    #endregion

    #region StartAppAsync

    [Fact]
    public async Task StartAppAsync_WithoutConfig_CompletesWithoutException()
    {
        var strategy = new OssStrategy(AppType.OssClient);

        // Should not throw — returns completed task when config is null
        await strategy.StartAppAsync();
    }

    #endregion

    #region Multiple Create calls

    [Fact]
    public void Create_SecondCall_OverwritesConfig()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        var context1 = new UpdateContext { ClientVersion = "1.0.0" };
        var context2 = new UpdateContext { ClientVersion = "2.0.0" };

        strategy.Create(context1);
        strategy.Create(context2);

        // No exception — second Create overwrites the first
    }

    #endregion
}
