using System;
using GeneralUpdate.Extension.Installation;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Installation
{
    /// <summary>
    /// Contains test cases for the InstalledExtension class.
    /// Tests the installed extension model properties and initialization.
    /// </summary>
    public class InstalledExtensionTests
    {
        /// <summary>
        /// Tests that a new InstalledExtension has Metadata initialized.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeDescriptor()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.NotNull(extension.Metadata);
        }

        /// <summary>
        /// Tests that a new InstalledExtension has InstallPath set to empty string by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetInstallPathToEmptyString()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.Equal(string.Empty, extension.InstallPath);
        }

        /// <summary>
        /// Tests that a new InstalledExtension has AutoUpdateEnabled set to true by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetAutoUpdateEnabledToTrue()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.True(extension.AutoUpdateEnabled);
        }

        /// <summary>
        /// Tests that a new InstalledExtension has IsEnabled set to true by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetIsEnabledToTrue()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.True(extension.IsEnabled);
        }

        /// <summary>
        /// Tests that a new InstalledExtension has LastUpdateDate set to null by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetLastUpdateDateToNull()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.Null(extension.LastUpdateDate);
        }

        /// <summary>
        /// Tests that Metadata property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Descriptor_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new InstalledExtension();
            var descriptor = new ExtensionMetadata
            {
                Name = "test-extension",
                Version = "1.0.0"
            };

            // Act
            extension.Metadata = descriptor;

            // Assert
            Assert.Same(descriptor, extension.Metadata);
        }

        /// <summary>
        /// Tests that InstallPath property can be set and retrieved.
        /// </summary>
        [Fact]
        public void InstallPath_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new InstalledExtension();
            var expectedPath = "/path/to/extension";

            // Act
            extension.InstallPath = expectedPath;

            // Assert
            Assert.Equal(expectedPath, extension.InstallPath);
        }

        /// <summary>
        /// Tests that InstallDate property can be set and retrieved.
        /// </summary>
        [Fact]
        public void InstallDate_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new InstalledExtension();
            var expectedDate = DateTime.Now;

            // Act
            extension.InstallDate = expectedDate;

            // Assert
            Assert.Equal(expectedDate, extension.InstallDate);
        }

        /// <summary>
        /// Tests that AutoUpdateEnabled property can be set to false.
        /// </summary>
        [Fact]
        public void AutoUpdateEnabled_CanBeSetToFalse()
        {
            // Arrange
            var extension = new InstalledExtension();

            // Act
            extension.AutoUpdateEnabled = false;

            // Assert
            Assert.False(extension.AutoUpdateEnabled);
        }

        /// <summary>
        /// Tests that IsEnabled property can be set to false.
        /// </summary>
        [Fact]
        public void IsEnabled_CanBeSetToFalse()
        {
            // Arrange
            var extension = new InstalledExtension();

            // Act
            extension.IsEnabled = false;

            // Assert
            Assert.False(extension.IsEnabled);
        }

        /// <summary>
        /// Tests that LastUpdateDate property can be set and retrieved.
        /// </summary>
        [Fact]
        public void LastUpdateDate_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new InstalledExtension();
            var expectedDate = DateTime.Now;

            // Act
            extension.LastUpdateDate = expectedDate;

            // Assert
            Assert.Equal(expectedDate, extension.LastUpdateDate);
        }

        /// <summary>
        /// Tests that LastUpdateDate can be set back to null after having a value.
        /// </summary>
        [Fact]
        public void LastUpdateDate_CanBeSetBackToNull()
        {
            // Arrange
            var extension = new InstalledExtension { LastUpdateDate = DateTime.Now };

            // Act
            extension.LastUpdateDate = null;

            // Assert
            Assert.Null(extension.LastUpdateDate);
        }

        /// <summary>
        /// Tests that all properties can be set together using object initializer.
        /// </summary>
        [Fact]
        public void AllProperties_CanBeSetUsingObjectInitializer()
        {
            // Arrange
            var installDate = new DateTime(2024, 1, 1);
            var lastUpdateDate = new DateTime(2024, 6, 1);

            // Act
            var extension = new InstalledExtension
            {
                Metadata = new ExtensionMetadata
                {
                    Name = "test-extension",
                    Version = "2.0.0"
                },
                InstallPath = "/usr/local/extensions/test",
                InstallDate = installDate,
                AutoUpdateEnabled = false,
                IsEnabled = false,
                LastUpdateDate = lastUpdateDate
            };

            // Assert
            Assert.Equal("test-extension", extension.Metadata.Name);
            Assert.Equal("2.0.0", extension.Metadata.Version);
            Assert.Equal("/usr/local/extensions/test", extension.InstallPath);
            Assert.Equal(installDate, extension.InstallDate);
            Assert.False(extension.AutoUpdateEnabled);
            Assert.False(extension.IsEnabled);
            Assert.Equal(lastUpdateDate, extension.LastUpdateDate);
        }

        /// <summary>
        /// Tests that InstallDate defaults to DateTime's default value (minimum date).
        /// </summary>
        [Fact]
        public void InstallDate_HasDefaultValue()
        {
            // Act
            var extension = new InstalledExtension();

            // Assert
            Assert.Equal(default(DateTime), extension.InstallDate);
        }

        /// <summary>
        /// Tests that boolean flags can be toggled multiple times.
        /// </summary>
        [Fact]
        public void BooleanFlags_CanBeToggledMultipleTimes()
        {
            // Arrange
            var extension = new InstalledExtension();

            // Act & Assert - Toggle AutoUpdateEnabled
            Assert.True(extension.AutoUpdateEnabled);
            extension.AutoUpdateEnabled = false;
            Assert.False(extension.AutoUpdateEnabled);
            extension.AutoUpdateEnabled = true;
            Assert.True(extension.AutoUpdateEnabled);

            // Act & Assert - Toggle IsEnabled
            Assert.True(extension.IsEnabled);
            extension.IsEnabled = false;
            Assert.False(extension.IsEnabled);
            extension.IsEnabled = true;
            Assert.True(extension.IsEnabled);
        }
    }
}
