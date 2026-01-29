using System;
using System.Collections.Generic;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Metadata
{
    /// <summary>
    /// Contains test cases for the ExtensionDescriptor class.
    /// Tests extension metadata properties, serialization attributes, and version parsing.
    /// </summary>
    public class ExtensionDescriptorTests
    {
        /// <summary>
        /// Tests that a new ExtensionDescriptor has default empty Name property.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeNameToEmptyString()
        {
            // Act
            var descriptor = new ExtensionDescriptor();

            // Assert
            Assert.Equal(string.Empty, descriptor.Name);
        }

        /// <summary>
        /// Tests that a new ExtensionDescriptor has default empty DisplayName property.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeDisplayNameToEmptyString()
        {
            // Act
            var descriptor = new ExtensionDescriptor();

            // Assert
            Assert.Equal(string.Empty, descriptor.DisplayName);
        }

        /// <summary>
        /// Tests that a new ExtensionDescriptor has default empty Version property.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeVersionToEmptyString()
        {
            // Act
            var descriptor = new ExtensionDescriptor();

            // Assert
            Assert.Equal(string.Empty, descriptor.Version);
        }

        /// <summary>
        /// Tests that a new ExtensionDescriptor has SupportedPlatforms set to All by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeSupportedPlatformsToAll()
        {
            // Act
            var descriptor = new ExtensionDescriptor();

            // Assert
            Assert.Equal(TargetPlatform.All, descriptor.SupportedPlatforms);
        }

        /// <summary>
        /// Tests that a new ExtensionDescriptor has Compatibility initialized.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeCompatibility()
        {
            // Act
            var descriptor = new ExtensionDescriptor();

            // Assert
            Assert.NotNull(descriptor.Compatibility);
        }

        /// <summary>
        /// Tests that Name property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Name_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedName = "my-awesome-extension";

            // Act
            descriptor.Name = expectedName;

            // Assert
            Assert.Equal(expectedName, descriptor.Name);
        }

        /// <summary>
        /// Tests that DisplayName property can be set and retrieved.
        /// </summary>
        [Fact]
        public void DisplayName_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedDisplayName = "My Awesome Extension";

            // Act
            descriptor.DisplayName = expectedDisplayName;

            // Assert
            Assert.Equal(expectedDisplayName, descriptor.DisplayName);
        }

        /// <summary>
        /// Tests that Version property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Version_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedVersion = "1.2.3";

            // Act
            descriptor.Version = expectedVersion;

            // Assert
            Assert.Equal(expectedVersion, descriptor.Version);
        }

        /// <summary>
        /// Tests that Description property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Description_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedDescription = "This is a test extension";

            // Act
            descriptor.Description = expectedDescription;

            // Assert
            Assert.Equal(expectedDescription, descriptor.Description);
        }

        /// <summary>
        /// Tests that Publisher property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Publisher_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedPublisher = "test-publisher";

            // Act
            descriptor.Publisher = expectedPublisher;

            // Assert
            Assert.Equal(expectedPublisher, descriptor.Publisher);
        }

        /// <summary>
        /// Tests that License property can be set and retrieved.
        /// </summary>
        [Fact]
        public void License_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedLicense = "MIT";

            // Act
            descriptor.License = expectedLicense;

            // Assert
            Assert.Equal(expectedLicense, descriptor.License);
        }

        /// <summary>
        /// Tests that Categories property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Categories_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedCategories = new List<string> { "Programming Languages", "Debuggers" };

            // Act
            descriptor.Categories = expectedCategories;

            // Assert
            Assert.Same(expectedCategories, descriptor.Categories);
        }

        /// <summary>
        /// Tests that SupportedPlatforms property can be set and retrieved.
        /// </summary>
        [Fact]
        public void SupportedPlatforms_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux;

            // Act
            descriptor.SupportedPlatforms = expectedPlatforms;

            // Assert
            Assert.Equal(expectedPlatforms, descriptor.SupportedPlatforms);
        }

        /// <summary>
        /// Tests that Compatibility property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Compatibility_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedCompatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0)
            };

            // Act
            descriptor.Compatibility = expectedCompatibility;

            // Assert
            Assert.Same(expectedCompatibility, descriptor.Compatibility);
        }

        /// <summary>
        /// Tests that DownloadUrl property can be set and retrieved.
        /// </summary>
        [Fact]
        public void DownloadUrl_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedUrl = "https://example.com/extension.zip";

            // Act
            descriptor.DownloadUrl = expectedUrl;

            // Assert
            Assert.Equal(expectedUrl, descriptor.DownloadUrl);
        }

        /// <summary>
        /// Tests that PackageHash property can be set and retrieved.
        /// </summary>
        [Fact]
        public void PackageHash_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedHash = "abc123def456";

            // Act
            descriptor.PackageHash = expectedHash;

            // Assert
            Assert.Equal(expectedHash, descriptor.PackageHash);
        }

        /// <summary>
        /// Tests that PackageSize property can be set and retrieved.
        /// </summary>
        [Fact]
        public void PackageSize_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedSize = 1024000L;

            // Act
            descriptor.PackageSize = expectedSize;

            // Assert
            Assert.Equal(expectedSize, descriptor.PackageSize);
        }

        /// <summary>
        /// Tests that ReleaseDate property can be set and retrieved.
        /// </summary>
        [Fact]
        public void ReleaseDate_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedDate = DateTime.Now;

            // Act
            descriptor.ReleaseDate = expectedDate;

            // Assert
            Assert.Equal(expectedDate, descriptor.ReleaseDate);
        }

        /// <summary>
        /// Tests that Dependencies property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Dependencies_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedDependencies = new List<string> { "extension1", "extension2" };

            // Act
            descriptor.Dependencies = expectedDependencies;

            // Assert
            Assert.Same(expectedDependencies, descriptor.Dependencies);
        }

        /// <summary>
        /// Tests that CustomProperties property can be set and retrieved.
        /// </summary>
        [Fact]
        public void CustomProperties_CanBeSetAndRetrieved()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor();
            var expectedProperties = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            // Act
            descriptor.CustomProperties = expectedProperties;

            // Assert
            Assert.Same(expectedProperties, descriptor.CustomProperties);
        }

        /// <summary>
        /// Tests that GetVersionObject returns a valid Version for a valid version string.
        /// </summary>
        [Fact]
        public void GetVersionObject_WithValidVersion_ReturnsVersionObject()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor { Version = "1.2.3" };

            // Act
            var version = descriptor.GetVersionObject();

            // Assert
            Assert.NotNull(version);
            Assert.Equal(1, version!.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Build);
        }

        /// <summary>
        /// Tests that GetVersionObject returns null for an invalid version string.
        /// </summary>
        [Fact]
        public void GetVersionObject_WithInvalidVersion_ReturnsNull()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor { Version = "invalid-version" };

            // Act
            var version = descriptor.GetVersionObject();

            // Assert
            Assert.Null(version);
        }

        /// <summary>
        /// Tests that GetVersionObject returns null for an empty version string.
        /// </summary>
        [Fact]
        public void GetVersionObject_WithEmptyVersion_ReturnsNull()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor { Version = string.Empty };

            // Act
            var version = descriptor.GetVersionObject();

            // Assert
            Assert.Null(version);
        }

        /// <summary>
        /// Tests that GetVersionObject handles complex version numbers with build and revision.
        /// </summary>
        [Fact]
        public void GetVersionObject_WithComplexVersion_ReturnsCorrectVersion()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor { Version = "1.2.3.4" };

            // Act
            var version = descriptor.GetVersionObject();

            // Assert
            Assert.NotNull(version);
            Assert.Equal(1, version!.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Build);
            Assert.Equal(4, version.Revision);
        }

        /// <summary>
        /// Tests that nullable properties can be set to null.
        /// </summary>
        [Fact]
        public void NullableProperties_CanBeSetToNull()
        {
            // Arrange
            var descriptor = new ExtensionDescriptor
            {
                Description = "Test",
                Publisher = "Publisher",
                License = "MIT",
                Categories = new List<string>(),
                DownloadUrl = "url",
                PackageHash = "hash",
                ReleaseDate = DateTime.Now,
                Dependencies = new List<string>(),
                CustomProperties = new Dictionary<string, string>()
            };

            // Act
            descriptor.Description = null;
            descriptor.Publisher = null;
            descriptor.License = null;
            descriptor.Categories = null;
            descriptor.DownloadUrl = null;
            descriptor.PackageHash = null;
            descriptor.ReleaseDate = null;
            descriptor.Dependencies = null;
            descriptor.CustomProperties = null;

            // Assert
            Assert.Null(descriptor.Description);
            Assert.Null(descriptor.Publisher);
            Assert.Null(descriptor.License);
            Assert.Null(descriptor.Categories);
            Assert.Null(descriptor.DownloadUrl);
            Assert.Null(descriptor.PackageHash);
            Assert.Null(descriptor.ReleaseDate);
            Assert.Null(descriptor.Dependencies);
            Assert.Null(descriptor.CustomProperties);
        }
    }
}
