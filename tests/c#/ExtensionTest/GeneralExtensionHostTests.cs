using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;
using Moq;

namespace ExtensionTest;

public class GeneralExtensionHostTests : IDisposable
{
    private readonly string _testExtensionsDirectory;
    private readonly Mock<IExtensionHttpClient> _httpClientMock;
    private readonly Mock<IExtensionCatalog> _catalogMock;
    private readonly Mock<IVersionCompatibilityChecker> _compatibilityCheckerMock;
    private readonly Mock<IDownloadQueueManager> _downloadQueueMock;
    private readonly Mock<IDependencyResolver> _dependencyResolverMock;
    private readonly Mock<IPlatformMatcher> _platformMatcherMock;

    public GeneralExtensionHostTests()
    {
        _testExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ExtHostTest_{Guid.NewGuid()}");
        _httpClientMock = new Mock<IExtensionHttpClient>();
        _catalogMock = new Mock<IExtensionCatalog>();
        _compatibilityCheckerMock = new Mock<IVersionCompatibilityChecker>();
        _downloadQueueMock = new Mock<IDownloadQueueManager>();
        _dependencyResolverMock = new Mock<IDependencyResolver>();
        _platformMatcherMock = new Mock<IPlatformMatcher>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testExtensionsDirectory))
        {
            Directory.Delete(_testExtensionsDirectory, true);
        }
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            null!,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        ));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenHttpClientNull()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            options,
            null!,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        ));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCatalogNull()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            null!,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        ));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCompatibilityCheckerNull()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            null!,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        ));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDownloadQueueNull()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            null!,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        ));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPlatformMatcherNull()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            null!
        ));
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithValidParameters()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Assert
        Assert.NotNull(host);
        Assert.NotNull(host.ExtensionCatalog);
    }

    [Fact]
    public void Constructor_ShouldLoadInstalledExtensions()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Assert
        _catalogMock.Verify(c => c.LoadInstalledExtensions(), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldCreateExtensionsDirectory()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Assert
        Assert.True(Directory.Exists(_testExtensionsDirectory));
    }

    [Fact]
    public void Constructor_ShouldCreateBackupDirectory()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Assert
        var backupDir = Path.Combine(_testExtensionsDirectory, ".backup");
        Assert.True(Directory.Exists(backupDir));
    }

    [Fact]
    public void IsExtensionCompatible_ShouldCallCompatibilityChecker()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0"
        };

        _compatibilityCheckerMock.Setup(c => c.IsCompatible(extension, "1.0.0"))
            .Returns(true);

        // Act
        var result = host.IsExtensionCompatible(extension);

        // Assert
        _compatibilityCheckerMock.Verify(c => c.IsCompatible(extension, "1.0.0"), Times.Once);
    }

    [Fact]
    public void IsExtensionCompatible_ShouldReturnTrue_WhenCompatible()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0"
        };

        _compatibilityCheckerMock.Setup(c => c.IsCompatible(extension, "1.0.0"))
            .Returns(true);

        // Act
        var result = host.IsExtensionCompatible(extension);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsExtensionCompatible_ShouldReturnFalse_WhenNotCompatible()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "2.0.0"
        };

        _compatibilityCheckerMock.Setup(c => c.IsCompatible(extension, "1.0.0"))
            .Returns(false);

        // Act
        var result = host.IsExtensionCompatible(extension);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetAutoUpdate_ShouldEnableAutoUpdate()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Act
        host.SetAutoUpdate("ext1", true);

        // Assert
        Assert.True(host.IsAutoUpdateEnabled("ext1"));
    }

    [Fact]
    public void SetAutoUpdate_ShouldDisableAutoUpdate()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Act
        host.SetAutoUpdate("ext1", true);
        host.SetAutoUpdate("ext1", false);

        // Assert
        Assert.False(host.IsAutoUpdateEnabled("ext1"));
    }

    [Fact]
    public void SetGlobalAutoUpdate_ShouldEnableGlobalAutoUpdate()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Act
        host.SetGlobalAutoUpdate(true);

        // Assert - Extension without specific setting should follow global setting
        Assert.True(host.IsAutoUpdateEnabled("any-extension"));
    }

    [Fact]
    public void SetGlobalAutoUpdate_ShouldDisableGlobalAutoUpdate()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Act
        host.SetGlobalAutoUpdate(true);
        host.SetGlobalAutoUpdate(false);

        // Assert
        Assert.False(host.IsAutoUpdateEnabled("any-extension"));
    }

    [Fact]
    public void IsAutoUpdateEnabled_ShouldReturnExtensionSpecificSetting()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        // Act
        host.SetGlobalAutoUpdate(false);
        host.SetAutoUpdate("ext1", true);

        // Assert
        Assert.True(host.IsAutoUpdateEnabled("ext1"));
        Assert.False(host.IsAutoUpdateEnabled("ext2"));
    }

    [Fact]
    public async Task InstallExtensionAsync_ShouldReturnFalse_WhenFileNotFound()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        var nonExistentPath = Path.Combine(_testExtensionsDirectory, "nonexistent.zip");

        // Act
        var result = await host.InstallExtensionAsync(nonExistentPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task InstallExtensionAsync_ShouldReturnFalse_WhenNotZipFile()
    {
        // Arrange
        var options = CreateTestOptions();
        var host = new GeneralExtensionHost(
            options,
            _httpClientMock.Object,
            _catalogMock.Object,
            _compatibilityCheckerMock.Object,
            _downloadQueueMock.Object,
            _dependencyResolverMock.Object,
            _platformMatcherMock.Object
        );

        var txtFilePath = Path.Combine(_testExtensionsDirectory, "test.txt");
        File.WriteAllText(txtFilePath, "test");

        // Act
        var result = await host.InstallExtensionAsync(txtFilePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void LegacyConstructor_ShouldInitialize_WithValidOptions()
    {
        // Arrange
        var options = new ExtensionHostOptions
        {
            HostVersion = "1.0.0",
            ExtensionsDirectory = _testExtensionsDirectory,
            ServerUrl = "https://example.com",
            Scheme = "Bearer",
            Token = "test-token"
        };

        // Act
        var host = new GeneralExtensionHost(options);

        // Assert
        Assert.NotNull(host);
        Assert.NotNull(host.ExtensionCatalog);
    }

    [Fact]
    public void LegacyConstructor_ShouldThrow_WhenOptionsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(null!));
    }

    private ExtensionHostOptions CreateTestOptions()
    {
        return new ExtensionHostOptions
        {
            HostVersion = "1.0.0",
            ExtensionsDirectory = _testExtensionsDirectory,
            ServerUrl = "https://example.com",
            Scheme = "Bearer",
            Token = "test-token"
        };
    }
}
