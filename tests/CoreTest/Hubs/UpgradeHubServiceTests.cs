using GeneralUpdate.Core.Hubs;

namespace CoreTest.Hubs;

/// <summary>
/// Unit tests for <see cref="UpgradeHubService"/> following AAAT pattern.
/// Tests the SignalR hub connection builder, listener registration, and lifecycle methods.
/// </summary>
public class UpgradeHubServiceTests
{
    private const string ValidUrl = "http://localhost:5000/UpgradeHub";

    #region Constructor

    [Fact]
    public void Ctor_WithValidUrl_CreatesInstance()
    {
        var service = new UpgradeHubService(ValidUrl);
        Assert.NotNull(service);
    }

    [Fact]
    public void Ctor_WithToken_CreatesInstance()
    {
        var service = new UpgradeHubService(ValidUrl, token: "test-token");
        Assert.NotNull(service);
    }

    [Fact]
    public void Ctor_WithAppKey_CreatesInstance()
    {
        var service = new UpgradeHubService(ValidUrl, appkey: "test-app-key");
        Assert.NotNull(service);
    }

    [Fact]
    public void Ctor_WithTokenAndAppKey_CreatesInstance()
    {
        var service = new UpgradeHubService(ValidUrl, token: "test-token", appkey: "test-app-key");
        Assert.NotNull(service);
    }

    [Fact]
    public void Ctor_WithNullToken_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl, token: null);
        Assert.NotNull(service);
    }

    [Fact]
    public void Ctor_WithNullAppKey_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl, appkey: null);
        Assert.NotNull(service);
    }

    #endregion

    #region AddListenerReceive

    [Fact]
    public void AddListenerReceive_WithValidCallback_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        var received = false;
        service.AddListenerReceive(_ => received = true);
        // Listener registered without exception
    }

    [Fact]
    public void AddListenerReceive_MultipleCallbacks_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        service.AddListenerReceive(_ => { });
        service.AddListenerReceive(_ => { });
    }

    #endregion

    #region AddListenerOnline

    [Fact]
    public void AddListenerOnline_WithValidCallback_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        var online = false;
        service.AddListenerOnline(_ => online = true);
    }

    [Fact]
    public void AddListenerOnline_MultipleCallbacks_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        service.AddListenerOnline(_ => { });
        service.AddListenerOnline(_ => { });
    }

    #endregion

    #region AddListenerReconnected

    [Fact]
    public void AddListenerReconnected_WithValidCallback_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        service.AddListenerReconnected(_ => Task.CompletedTask);
    }

    [Fact]
    public void AddListenerReconnected_WithNullCallback_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        service.AddListenerReconnected(null);
    }

    #endregion

    #region AddListenerClosed

    [Fact]
    public void AddListenerClosed_WithValidCallback_DoesNotThrow()
    {
        var service = new UpgradeHubService(ValidUrl);
        service.AddListenerClosed(_ => Task.CompletedTask);
    }

    #endregion

    #region IUpgradeHubService contract

    [Fact]
    public void Implements_IUpgradeHubService()
    {
        var service = new UpgradeHubService(ValidUrl);
        Assert.IsAssignableFrom<IUpgradeHubService>(service);
    }

    #endregion
}
