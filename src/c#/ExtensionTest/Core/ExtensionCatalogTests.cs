using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Installation;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Core
{
    /// <summary>
    /// Contains test cases for the ExtensionCatalog class.
    /// Tests catalog management, extension loading, filtering, and persistence.
    /// </summary>
    public class ExtensionCatalogTests : IDisposable
    {
        private readonly string _testInstallPath;

        /// <summary>
        /// Initializes a new instance of the test class with a temporary test directory.
        /// </summary>
        public ExtensionCatalogTests()
        {
            _testInstallPath = Path.Combine(Path.GetTempPath(), $"ExtensionTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testInstallPath);
        }

        /// <summary>
        /// Cleans up the temporary test directory after tests complete.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_testInstallPath))
            {
                Directory.Delete(_testInstallPath, recursive: true);
            }
        }

        /// <summary>
        /// Helper method to create a test extension.
        /// </summary>
        private InstalledExtension CreateTestExtension(string name, TargetPlatform platform = TargetPlatform.All)
        {
            return new InstalledExtension
            {
                Descriptor = new ExtensionDescriptor
                {
                    Name = name,
                    DisplayName = $"Test {name}",
                    Version = "1.0.0",
                    SupportedPlatforms = platform
                },
                InstallPath = Path.Combine(_testInstallPath, name),
                InstallDate = DateTime.Now,
                AutoUpdateEnabled = true,
                IsEnabled = true
            };
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when installBasePath is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExtensionCatalog(null!));
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when installBasePath is empty.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExtensionCatalog(string.Empty));
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when installBasePath is whitespace.
        /// </summary>
        [Fact]
        public void Constructor_WithWhitespacePath_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExtensionCatalog("   "));
        }

        /// <summary>
        /// Tests that constructor creates the install directory if it doesn't exist.
        /// </summary>
        [Fact]
        public void Constructor_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var newPath = Path.Combine(_testInstallPath, "new-dir");

            // Act
            var catalog = new ExtensionCatalog(newPath);

            // Assert
            Assert.True(Directory.Exists(newPath));

            // Cleanup
            Directory.Delete(newPath);
        }

        /// <summary>
        /// Tests that GetInstalledExtensions returns empty list initially.
        /// </summary>
        [Fact]
        public void GetInstalledExtensions_InitiallyReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            var extensions = catalog.GetInstalledExtensions();

            // Assert
            Assert.Empty(extensions);
        }

        /// <summary>
        /// Tests that GetInstalledExtensions returns a defensive copy.
        /// </summary>
        [Fact]
        public void GetInstalledExtensions_ReturnsDefensiveCopy()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extension = CreateTestExtension("test-ext");
            catalog.AddOrUpdateInstalledExtension(extension);

            // Act
            var list1 = catalog.GetInstalledExtensions();
            var list2 = catalog.GetInstalledExtensions();

            // Assert
            Assert.NotSame(list1, list2);
        }

        /// <summary>
        /// Tests that AddOrUpdateInstalledExtension throws ArgumentNullException when extension is null.
        /// </summary>
        [Fact]
        public void AddOrUpdateInstalledExtension_WithNullExtension_ThrowsArgumentNullException()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => catalog.AddOrUpdateInstalledExtension(null!));
        }

        /// <summary>
        /// Tests that AddOrUpdateInstalledExtension adds a new extension.
        /// </summary>
        [Fact]
        public void AddOrUpdateInstalledExtension_AddsNewExtension()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extension = CreateTestExtension("new-ext");
            Directory.CreateDirectory(extension.InstallPath);

            // Act
            catalog.AddOrUpdateInstalledExtension(extension);

            // Assert
            var extensions = catalog.GetInstalledExtensions();
            Assert.Single(extensions);
            Assert.Equal("new-ext", extensions[0].Descriptor.Name);
        }

        /// <summary>
        /// Tests that AddOrUpdateInstalledExtension updates an existing extension.
        /// </summary>
        [Fact]
        public void AddOrUpdateInstalledExtension_UpdatesExistingExtension()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extension1 = CreateTestExtension("test-ext");
            extension1.Descriptor.Version = "1.0.0";
            Directory.CreateDirectory(extension1.InstallPath);

            var extension2 = CreateTestExtension("test-ext");
            extension2.Descriptor.Version = "2.0.0";
            extension2.InstallPath = extension1.InstallPath;

            // Act
            catalog.AddOrUpdateInstalledExtension(extension1);
            catalog.AddOrUpdateInstalledExtension(extension2);

            // Assert
            var extensions = catalog.GetInstalledExtensions();
            Assert.Single(extensions);
            Assert.Equal("2.0.0", extensions[0].Descriptor.Version);
        }

        /// <summary>
        /// Tests that GetInstalledExtensionById returns null when extension not found.
        /// </summary>
        [Fact]
        public void GetInstalledExtensionById_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            var extension = catalog.GetInstalledExtensionById("non-existent");

            // Assert
            Assert.Null(extension);
        }

        /// <summary>
        /// Tests that GetInstalledExtensionById returns the correct extension.
        /// </summary>
        [Fact]
        public void GetInstalledExtensionById_WithValidId_ReturnsExtension()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extension = CreateTestExtension("test-ext");
            Directory.CreateDirectory(extension.InstallPath);
            catalog.AddOrUpdateInstalledExtension(extension);

            // Act
            var found = catalog.GetInstalledExtensionById("test-ext");

            // Assert
            Assert.NotNull(found);
            Assert.Equal("test-ext", found!.Descriptor.Name);
        }

        /// <summary>
        /// Tests that GetInstalledExtensionsByPlatform filters correctly for Windows.
        /// </summary>
        [Fact]
        public void GetInstalledExtensionsByPlatform_FiltersForWindows()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var ext1 = CreateTestExtension("windows-ext", TargetPlatform.Windows);
            var ext2 = CreateTestExtension("linux-ext", TargetPlatform.Linux);
            var ext3 = CreateTestExtension("all-ext", TargetPlatform.All);

            Directory.CreateDirectory(ext1.InstallPath);
            Directory.CreateDirectory(ext2.InstallPath);
            Directory.CreateDirectory(ext3.InstallPath);

            catalog.AddOrUpdateInstalledExtension(ext1);
            catalog.AddOrUpdateInstalledExtension(ext2);
            catalog.AddOrUpdateInstalledExtension(ext3);

            // Act
            var windowsExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Windows);

            // Assert
            Assert.Equal(2, windowsExtensions.Count);
            Assert.Contains(windowsExtensions, e => e.Descriptor.Name == "windows-ext");
            Assert.Contains(windowsExtensions, e => e.Descriptor.Name == "all-ext");
        }

        /// <summary>
        /// Tests that GetInstalledExtensionsByPlatform filters correctly for Linux.
        /// </summary>
        [Fact]
        public void GetInstalledExtensionsByPlatform_FiltersForLinux()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var ext1 = CreateTestExtension("windows-ext", TargetPlatform.Windows);
            var ext2 = CreateTestExtension("linux-ext", TargetPlatform.Linux);
            var ext3 = CreateTestExtension("all-ext", TargetPlatform.All);

            Directory.CreateDirectory(ext1.InstallPath);
            Directory.CreateDirectory(ext2.InstallPath);
            Directory.CreateDirectory(ext3.InstallPath);

            catalog.AddOrUpdateInstalledExtension(ext1);
            catalog.AddOrUpdateInstalledExtension(ext2);
            catalog.AddOrUpdateInstalledExtension(ext3);

            // Act
            var linuxExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Linux);

            // Assert
            Assert.Equal(2, linuxExtensions.Count);
            Assert.Contains(linuxExtensions, e => e.Descriptor.Name == "linux-ext");
            Assert.Contains(linuxExtensions, e => e.Descriptor.Name == "all-ext");
        }

        /// <summary>
        /// Tests that GetInstalledExtensionsByPlatform returns empty list when no matches found.
        /// </summary>
        [Fact]
        public void GetInstalledExtensionsByPlatform_WithNoMatches_ReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var ext = CreateTestExtension("windows-ext", TargetPlatform.Windows);
            Directory.CreateDirectory(ext.InstallPath);
            catalog.AddOrUpdateInstalledExtension(ext);

            // Act
            var linuxExtensions = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.MacOS);

            // Assert
            Assert.Empty(linuxExtensions);
        }

        /// <summary>
        /// Tests that RemoveInstalledExtension removes the extension from catalog.
        /// </summary>
        [Fact]
        public void RemoveInstalledExtension_RemovesExtensionFromCatalog()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extension = CreateTestExtension("test-ext");
            Directory.CreateDirectory(extension.InstallPath);
            catalog.AddOrUpdateInstalledExtension(extension);

            // Act
            catalog.RemoveInstalledExtension("test-ext");

            // Assert
            var extensions = catalog.GetInstalledExtensions();
            Assert.Empty(extensions);
        }

        /// <summary>
        /// Tests that RemoveInstalledExtension with non-existent extension does nothing.
        /// </summary>
        [Fact]
        public void RemoveInstalledExtension_WithNonExistentExtension_DoesNothing()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            catalog.RemoveInstalledExtension("non-existent");

            // Assert - no exception should be thrown
            Assert.Empty(catalog.GetInstalledExtensions());
        }

        /// <summary>
        /// Tests that ParseAvailableExtensions returns empty list for null JSON.
        /// </summary>
        [Fact]
        public void ParseAvailableExtensions_WithNullJson_ReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            var extensions = catalog.ParseAvailableExtensions(null!);

            // Assert
            Assert.Empty(extensions);
        }

        /// <summary>
        /// Tests that ParseAvailableExtensions returns empty list for empty JSON.
        /// </summary>
        [Fact]
        public void ParseAvailableExtensions_WithEmptyJson_ReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            var extensions = catalog.ParseAvailableExtensions(string.Empty);

            // Assert
            Assert.Empty(extensions);
        }

        /// <summary>
        /// Tests that ParseAvailableExtensions parses valid JSON correctly.
        /// </summary>
        [Fact]
        public void ParseAvailableExtensions_WithValidJson_ParsesCorrectly()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var testExtensions = new List<AvailableExtension>
            {
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "ext1",
                        Version = "1.0.0"
                    }
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "ext2",
                        Version = "2.0.0"
                    }
                }
            };
            var json = JsonSerializer.Serialize(testExtensions);

            // Act
            var extensions = catalog.ParseAvailableExtensions(json);

            // Assert
            Assert.Equal(2, extensions.Count);
            Assert.Equal("ext1", extensions[0].Descriptor.Name);
            Assert.Equal("ext2", extensions[1].Descriptor.Name);
        }

        /// <summary>
        /// Tests that ParseAvailableExtensions returns empty list for invalid JSON.
        /// </summary>
        [Fact]
        public void ParseAvailableExtensions_WithInvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var invalidJson = "{ invalid json }";

            // Act
            var extensions = catalog.ParseAvailableExtensions(invalidJson);

            // Assert
            Assert.Empty(extensions);
        }

        /// <summary>
        /// Tests that FilterByPlatform returns empty list when input is null.
        /// </summary>
        [Fact]
        public void FilterByPlatform_WithNullList_ReturnsEmptyList()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);

            // Act
            var filtered = catalog.FilterByPlatform(null!, TargetPlatform.Windows);

            // Assert
            Assert.Empty(filtered);
        }

        /// <summary>
        /// Tests that FilterByPlatform filters extensions correctly.
        /// </summary>
        [Fact]
        public void FilterByPlatform_FiltersCorrectly()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extensions = new List<AvailableExtension>
            {
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "windows-ext",
                        SupportedPlatforms = TargetPlatform.Windows
                    }
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "linux-ext",
                        SupportedPlatforms = TargetPlatform.Linux
                    }
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "all-ext",
                        SupportedPlatforms = TargetPlatform.All
                    }
                }
            };

            // Act
            var windowsExtensions = catalog.FilterByPlatform(extensions, TargetPlatform.Windows);

            // Assert
            Assert.Equal(2, windowsExtensions.Count);
            Assert.Contains(windowsExtensions, e => e.Descriptor.Name == "windows-ext");
            Assert.Contains(windowsExtensions, e => e.Descriptor.Name == "all-ext");
        }

        /// <summary>
        /// Tests that FilterByPlatform handles combined platform flags.
        /// </summary>
        [Fact]
        public void FilterByPlatform_HandlesCombinedFlags()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var extensions = new List<AvailableExtension>
            {
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "win-linux-ext",
                        SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux
                    }
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "mac-only-ext",
                        SupportedPlatforms = TargetPlatform.MacOS
                    }
                }
            };

            // Act
            var windowsExtensions = catalog.FilterByPlatform(extensions, TargetPlatform.Windows);
            var macExtensions = catalog.FilterByPlatform(extensions, TargetPlatform.MacOS);

            // Assert
            Assert.Single(windowsExtensions);
            Assert.Equal("win-linux-ext", windowsExtensions[0].Descriptor.Name);
            Assert.Single(macExtensions);
            Assert.Equal("mac-only-ext", macExtensions[0].Descriptor.Name);
        }

        /// <summary>
        /// Tests that LoadInstalledExtensions loads extensions from manifest files.
        /// </summary>
        [Fact]
        public void LoadInstalledExtensions_LoadsExtensionsFromManifests()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var ext1Path = Path.Combine(_testInstallPath, "ext1");
            var ext2Path = Path.Combine(_testInstallPath, "ext2");
            Directory.CreateDirectory(ext1Path);
            Directory.CreateDirectory(ext2Path);

            var ext1 = CreateTestExtension("ext1");
            ext1.InstallPath = ext1Path;
            var ext2 = CreateTestExtension("ext2");
            ext2.InstallPath = ext2Path;

            var manifest1 = JsonSerializer.Serialize(ext1);
            var manifest2 = JsonSerializer.Serialize(ext2);
            File.WriteAllText(Path.Combine(ext1Path, "manifest.json"), manifest1);
            File.WriteAllText(Path.Combine(ext2Path, "manifest.json"), manifest2);

            // Act
            catalog.LoadInstalledExtensions();

            // Assert
            var extensions = catalog.GetInstalledExtensions();
            Assert.Equal(2, extensions.Count);
        }

        /// <summary>
        /// Tests that LoadInstalledExtensions clears existing extensions before loading.
        /// </summary>
        [Fact]
        public void LoadInstalledExtensions_ClearsExistingExtensions()
        {
            // Arrange
            var catalog = new ExtensionCatalog(_testInstallPath);
            var ext1 = CreateTestExtension("ext1");
            Directory.CreateDirectory(ext1.InstallPath);
            catalog.AddOrUpdateInstalledExtension(ext1);

            // Verify extension was added
            Assert.Single(catalog.GetInstalledExtensions());

            // Delete the manifest file so LoadInstalledExtensions finds nothing
            var manifestPath = Path.Combine(ext1.InstallPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            // Act
            catalog.LoadInstalledExtensions();

            // Assert - Should be empty since no manifest files exist in filesystem
            var extensions = catalog.GetInstalledExtensions();
            Assert.Empty(extensions);
        }
    }
}
