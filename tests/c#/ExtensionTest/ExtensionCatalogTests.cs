using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using Newtonsoft.Json;

namespace ExtensionTest;

public class ExtensionCatalogTests : IDisposable
{
    private readonly string _testCatalogPath;

    public ExtensionCatalogTests()
    {
        _testCatalogPath = Path.Combine(Path.GetTempPath(), $"ExtensionTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testCatalogPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testCatalogPath))
        {
            Directory.Delete(_testCatalogPath, true);
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeCatalog()
    {
        // Arrange & Act
        var catalog = new ExtensionCatalog(_testCatalogPath);

        // Assert
        Assert.NotNull(catalog);
    }

    [Fact]
    public void LoadInstalledExtensions_ShouldLoadFromDirectory()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        CreateManifestFile(extension);

        // Act
        catalog.LoadInstalledExtensions();
        var extensions = catalog.GetInstalledExtensions();

        // Assert
        Assert.Single(extensions);
        Assert.Equal("ext1", extensions[0].Id);
        Assert.Equal("TestExtension", extensions[0].Name);
    }

    [Fact]
    public void LoadInstalledExtensions_ShouldSkipBackupDirectory()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        CreateManifestFile(extension);

        // Create a backup directory that should be skipped
        var backupDir = Path.Combine(_testCatalogPath, ".backup");
        Directory.CreateDirectory(backupDir);
        var backupExtension = CreateTestExtension("backup1", "BackupExtension");
        var backupManifest = Path.Combine(backupDir, "manifest.json");
        File.WriteAllText(backupManifest, JsonConvert.SerializeObject(backupExtension));

        // Act
        catalog.LoadInstalledExtensions();
        var extensions = catalog.GetInstalledExtensions();

        // Assert
        Assert.Single(extensions);
        Assert.Equal("ext1", extensions[0].Id);
    }

    [Fact]
    public void GetInstalledExtensions_ShouldReturnAllExtensions()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var ext1 = CreateTestExtension("ext1", "Extension1");
        var ext2 = CreateTestExtension("ext2", "Extension2");
        CreateManifestFile(ext1);
        CreateManifestFile(ext2);
        catalog.LoadInstalledExtensions();

        // Act
        var extensions = catalog.GetInstalledExtensions();

        // Assert
        Assert.Equal(2, extensions.Count);
        Assert.Contains(extensions, e => e.Id == "ext1");
        Assert.Contains(extensions, e => e.Id == "ext2");
    }

    [Fact]
    public void GetInstalledExtensionsByPlatform_ShouldFilterByPlatform()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var ext1 = CreateTestExtension("ext1", "WindowsExt", TargetPlatform.Windows);
        var ext2 = CreateTestExtension("ext2", "LinuxExt", TargetPlatform.Linux);
        var ext3 = CreateTestExtension("ext3", "AllExt", TargetPlatform.All);
        CreateManifestFile(ext1);
        CreateManifestFile(ext2);
        CreateManifestFile(ext3);
        catalog.LoadInstalledExtensions();

        // Act
        var windowsExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Windows);

        // Assert
        Assert.Equal(2, windowsExtensions.Count);
        Assert.Contains(windowsExtensions, e => e.Id == "ext1");
        Assert.Contains(windowsExtensions, e => e.Id == "ext3");
    }

    [Fact]
    public void GetInstalledExtensionById_ShouldReturnExtension()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        CreateManifestFile(extension);
        catalog.LoadInstalledExtensions();

        // Act
        var result = catalog.GetInstalledExtensionById("ext1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ext1", result.Id);
        Assert.Equal("TestExtension", result.Name);
    }

    [Fact]
    public void GetInstalledExtensionById_ShouldReturnNullForNonExistent()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        catalog.LoadInstalledExtensions();

        // Act
        var result = catalog.GetInstalledExtensionById("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AddOrUpdateInstalledExtension_ShouldAddNewExtension()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");

        // Act
        catalog.AddOrUpdateInstalledExtension(extension);
        var result = catalog.GetInstalledExtensionById("ext1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ext1", result.Id);
    }

    [Fact]
    public void AddOrUpdateInstalledExtension_ShouldUpdateExistingExtension()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        catalog.AddOrUpdateInstalledExtension(extension);

        // Act
        extension.Version = "2.0.0";
        catalog.AddOrUpdateInstalledExtension(extension);
        var result = catalog.GetInstalledExtensionById("ext1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void AddOrUpdateInstalledExtension_ShouldCreateManifestFile()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");

        // Act
        catalog.AddOrUpdateInstalledExtension(extension);
        var manifestPath = Path.Combine(_testCatalogPath, "TestExtension", "manifest.json");

        // Assert
        Assert.True(File.Exists(manifestPath));
    }

    [Fact]
    public void RemoveInstalledExtension_ShouldRemoveExtension()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        catalog.AddOrUpdateInstalledExtension(extension);

        // Act
        catalog.RemoveInstalledExtension("ext1");
        var result = catalog.GetInstalledExtensionById("ext1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RemoveInstalledExtension_ShouldRemoveDirectory()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);
        var extension = CreateTestExtension("ext1", "TestExtension");
        catalog.AddOrUpdateInstalledExtension(extension);
        var extensionDir = Path.Combine(_testCatalogPath, "TestExtension");

        // Act
        catalog.RemoveInstalledExtension("ext1");

        // Assert
        Assert.False(Directory.Exists(extensionDir));
    }

    [Fact]
    public void RemoveInstalledExtension_ShouldNotThrowForNonExistent()
    {
        // Arrange
        var catalog = new ExtensionCatalog(_testCatalogPath);

        // Act & Assert
        var exception = Record.Exception(() => catalog.RemoveInstalledExtension("nonexistent"));
        Assert.Null(exception);
    }

    [Fact]
    public void LoadInstalledExtensions_ShouldHandleNonExistentDirectory()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}");
        var catalog = new ExtensionCatalog(nonExistentPath);

        // Act & Assert
        var exception = Record.Exception(() => catalog.LoadInstalledExtensions());
        Assert.Null(exception);
    }

    private ExtensionMetadata CreateTestExtension(
        string id, 
        string name, 
        TargetPlatform platform = TargetPlatform.All)
    {
        return new ExtensionMetadata
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Version = "1.0.0",
            Description = $"Test extension {name}",
            SupportedPlatforms = platform,
            Status = true
        };
    }

    private void CreateManifestFile(ExtensionMetadata extension)
    {
        var extensionDir = Path.Combine(_testCatalogPath, extension.Name!);
        Directory.CreateDirectory(extensionDir);
        var manifestPath = Path.Combine(extensionDir, "manifest.json");
        var json = JsonConvert.SerializeObject(extension, Formatting.Indented);
        File.WriteAllText(manifestPath, json);
    }
}
