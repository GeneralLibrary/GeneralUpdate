using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;
using Moq;

namespace ExtensionTest.Core;

/// <summary>
/// Unit tests for <see cref="ExtensionServiceFactory"/> following AAAT pattern.
/// Tests all factory methods and interface contract.
/// </summary>
public class ExtensionServiceFactoryTests
{
    private readonly ExtensionServiceFactory _factory = new();

    #region IExtensionServiceFactory contract

    [Fact]
    public void Implements_IExtensionServiceFactory()
    {
        Assert.IsAssignableFrom<IExtensionServiceFactory>(_factory);
    }

    #endregion

    #region CreateHttpClient

    [Fact]
    public void CreateHttpClient_ThrowsNotSupportedException()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _factory.CreateHttpClient());
        Assert.Contains("ExtensionHostBuilder", ex.Message);
    }

    #endregion

    #region CreateCompatibilityChecker

    [Fact]
    public void CreateCompatibilityChecker_ReturnsVersionCompatibilityChecker()
    {
        var checker = _factory.CreateCompatibilityChecker();
        Assert.NotNull(checker);
        Assert.IsAssignableFrom<IVersionCompatibilityChecker>(checker);
    }

    [Fact]
    public void CreateCompatibilityChecker_MultipleCalls_ReturnDifferentInstances()
    {
        var checker1 = _factory.CreateCompatibilityChecker();
        var checker2 = _factory.CreateCompatibilityChecker();

        Assert.NotSame(checker1, checker2);
    }

    #endregion

    #region CreateDownloadQueueManager

    [Fact]
    public void CreateDownloadQueueManager_ReturnsDownloadQueueManager()
    {
        var manager = _factory.CreateDownloadQueueManager();
        Assert.NotNull(manager);
        Assert.IsAssignableFrom<IDownloadQueueManager>(manager);
    }

    [Fact]
    public void CreateDownloadQueueManager_MultipleCalls_ReturnDifferentInstances()
    {
        var mgr1 = _factory.CreateDownloadQueueManager();
        var mgr2 = _factory.CreateDownloadQueueManager();

        Assert.NotSame(mgr1, mgr2);
    }

    #endregion

    #region CreateDependencyResolver

    [Fact]
    public void CreateDependencyResolver_WithCatalog_ReturnsDependencyResolver()
    {
        var catalogMock = new Mock<IExtensionCatalog>();

        var resolver = _factory.CreateDependencyResolver(catalogMock.Object);

        Assert.NotNull(resolver);
        Assert.IsAssignableFrom<IDependencyResolver>(resolver);
    }

    [Fact]
    public void CreateDependencyResolver_NullCatalog_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.CreateDependencyResolver(null!));
    }

    #endregion

    #region CreatePlatformMatcher

    [Fact]
    public void CreatePlatformMatcher_ReturnsPlatformMatcher()
    {
        var matcher = _factory.CreatePlatformMatcher();
        Assert.NotNull(matcher);
        Assert.IsAssignableFrom<IPlatformMatcher>(matcher);
    }

    [Fact]
    public void CreatePlatformMatcher_MultipleCalls_ReturnDifferentInstances()
    {
        var m1 = _factory.CreatePlatformMatcher();
        var m2 = _factory.CreatePlatformMatcher();

        Assert.NotSame(m1, m2);
    }

    #endregion
}
