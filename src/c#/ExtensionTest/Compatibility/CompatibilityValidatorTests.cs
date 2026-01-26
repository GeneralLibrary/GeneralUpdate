using System;
using System.Collections.Generic;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Installation;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Compatibility
{
    /// <summary>
    /// Contains test cases for the CompatibilityValidator class.
    /// Tests version compatibility validation, filtering, and update detection logic.
    /// </summary>
    public class CompatibilityValidatorTests
    {
        /// <summary>
        /// Helper method to create a test extension with specific version constraints.
        /// </summary>
        private ExtensionDescriptor CreateDescriptor(string version, Version? minHost = null, Version? maxHost = null)
        {
            return new ExtensionDescriptor
            {
                Name = "test-extension",
                Version = version,
                Compatibility = new VersionCompatibility
                {
                    MinHostVersion = minHost,
                    MaxHostVersion = maxHost
                }
            };
        }

        /// <summary>
        /// Helper method to create an AvailableExtension with specific version.
        /// </summary>
        private AvailableExtension CreateAvailableExtension(string version, Version? minHost = null, Version? maxHost = null)
        {
            return new AvailableExtension
            {
                Descriptor = CreateDescriptor(version, minHost, maxHost)
            };
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when hostVersion is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullHostVersion_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CompatibilityValidator(null!));
        }

        /// <summary>
        /// Tests that constructor accepts a valid host version.
        /// </summary>
        [Fact]
        public void Constructor_WithValidHostVersion_CreatesInstance()
        {
            // Arrange
            var hostVersion = new Version(1, 0, 0);

            // Act
            var validator = new CompatibilityValidator(hostVersion);

            // Assert
            Assert.NotNull(validator);
        }

        /// <summary>
        /// Tests that IsCompatible returns true when no version constraints are set.
        /// </summary>
        [Fact]
        public void IsCompatible_WithNoConstraints_ReturnsTrue()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var descriptor = CreateDescriptor("1.0.0");

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatible throws ArgumentNullException when descriptor is null.
        /// </summary>
        [Fact]
        public void IsCompatible_WithNullDescriptor_ThrowsArgumentNullException()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => validator.IsCompatible(null!));
        }

        /// <summary>
        /// Tests that IsCompatible returns true when host meets minimum version.
        /// </summary>
        [Fact]
        public void IsCompatible_HostMeetsMinimum_ReturnsTrue()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var descriptor = CreateDescriptor("1.0.0", minHost: new Version(1, 0, 0));

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatible returns false when host is below minimum version.
        /// </summary>
        [Fact]
        public void IsCompatible_HostBelowMinimum_ReturnsFalse()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var descriptor = CreateDescriptor("1.0.0", minHost: new Version(2, 0, 0));

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatible returns true when host is within maximum version.
        /// </summary>
        [Fact]
        public void IsCompatible_HostWithinMaximum_ReturnsTrue()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var descriptor = CreateDescriptor("1.0.0", maxHost: new Version(3, 0, 0));

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatible returns false when host exceeds maximum version.
        /// </summary>
        [Fact]
        public void IsCompatible_HostExceedsMaximum_ReturnsFalse()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(3, 0, 0));
            var descriptor = CreateDescriptor("1.0.0", maxHost: new Version(2, 0, 0));

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatible returns true when host is within version range.
        /// </summary>
        [Fact]
        public void IsCompatible_HostWithinRange_ReturnsTrue()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var descriptor = CreateDescriptor("1.0.0",
                minHost: new Version(1, 0, 0),
                maxHost: new Version(3, 0, 0));

            // Act
            var result = validator.IsCompatible(descriptor);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that FilterCompatible returns empty list when input is null.
        /// </summary>
        [Fact]
        public void FilterCompatible_WithNullList_ReturnsEmptyList()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));

            // Act
            var result = validator.FilterCompatible(null!);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that FilterCompatible returns only compatible extensions.
        /// </summary>
        [Fact]
        public void FilterCompatible_FiltersIncompatibleExtensions()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var extensions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(1, 0, 0)),  // Compatible
                CreateAvailableExtension("2.0.0", minHost: new Version(3, 0, 0)),  // Not compatible
                CreateAvailableExtension("3.0.0", minHost: new Version(1, 0, 0))   // Compatible
            };

            // Act
            var result = validator.FilterCompatible(extensions);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("1.0.0", result[0].Descriptor.Version);
            Assert.Equal("3.0.0", result[1].Descriptor.Version);
        }

        /// <summary>
        /// Tests that FilterCompatible handles empty list.
        /// </summary>
        [Fact]
        public void FilterCompatible_WithEmptyList_ReturnsEmptyList()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var extensions = new List<AvailableExtension>();

            // Act
            var result = validator.FilterCompatible(extensions);

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that FindLatestCompatible returns null when list is null.
        /// </summary>
        [Fact]
        public void FindLatestCompatible_WithNullList_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));

            // Act
            var result = validator.FindLatestCompatible(null!);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FindLatestCompatible returns null when list is empty.
        /// </summary>
        [Fact]
        public void FindLatestCompatible_WithEmptyList_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var extensions = new List<AvailableExtension>();

            // Act
            var result = validator.FindLatestCompatible(extensions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FindLatestCompatible returns the highest compatible version.
        /// </summary>
        [Fact]
        public void FindLatestCompatible_ReturnsHighestCompatibleVersion()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var extensions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("2.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("3.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("4.0.0", minHost: new Version(3, 0, 0))  // Not compatible
            };

            // Act
            var result = validator.FindLatestCompatible(extensions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("3.0.0", result!.Descriptor.Version);
        }

        /// <summary>
        /// Tests that FindLatestCompatible returns null when no compatible versions exist.
        /// </summary>
        [Fact]
        public void FindLatestCompatible_WithNoCompatibleVersions_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var extensions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(2, 0, 0)),
                CreateAvailableExtension("2.0.0", minHost: new Version(3, 0, 0))
            };

            // Act
            var result = validator.FindLatestCompatible(extensions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FindMinimumSupportedLatest returns null when list is null.
        /// </summary>
        [Fact]
        public void FindMinimumSupportedLatest_WithNullList_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));

            // Act
            var result = validator.FindMinimumSupportedLatest(null!);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FindMinimumSupportedLatest returns null when list is empty.
        /// </summary>
        [Fact]
        public void FindMinimumSupportedLatest_WithEmptyList_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var extensions = new List<AvailableExtension>();

            // Act
            var result = validator.FindMinimumSupportedLatest(extensions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FindMinimumSupportedLatest returns the maximum compatible version.
        /// </summary>
        [Fact]
        public void FindMinimumSupportedLatest_ReturnsMaximumCompatibleVersion()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var extensions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("2.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("3.0.0", minHost: new Version(1, 0, 0))
            };

            // Act
            var result = validator.FindMinimumSupportedLatest(extensions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("3.0.0", result!.Descriptor.Version);
        }

        /// <summary>
        /// Tests that FindMinimumSupportedLatest returns null when no compatible versions exist.
        /// </summary>
        [Fact]
        public void FindMinimumSupportedLatest_WithNoCompatibleVersions_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var extensions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(2, 0, 0))
            };

            // Act
            var result = validator.FindMinimumSupportedLatest(extensions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns null when installed is null.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_WithNullInstalled_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var availableVersions = new List<AvailableExtension>();

            // Act
            var result = validator.GetCompatibleUpdate(null!, availableVersions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns null when availableVersions is null.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_WithNullAvailableVersions_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = CreateDescriptor("1.0.0")
            };

            // Act
            var result = validator.GetCompatibleUpdate(installed, null!);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns null when availableVersions is empty.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_WithEmptyAvailableVersions_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(1, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = CreateDescriptor("1.0.0")
            };
            var availableVersions = new List<AvailableExtension>();

            // Act
            var result = validator.GetCompatibleUpdate(installed, availableVersions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns the latest newer compatible version.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_ReturnsLatestNewerCompatibleVersion()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = CreateDescriptor("1.0.0")
            };
            var availableVersions = new List<AvailableExtension>
            {
                CreateAvailableExtension("0.9.0", minHost: new Version(1, 0, 0)),  // Older version
                CreateAvailableExtension("1.0.0", minHost: new Version(1, 0, 0)),  // Same version
                CreateAvailableExtension("1.5.0", minHost: new Version(1, 0, 0)),  // Newer compatible
                CreateAvailableExtension("2.0.0", minHost: new Version(1, 0, 0))   // Latest compatible
            };

            // Act
            var result = validator.GetCompatibleUpdate(installed, availableVersions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2.0.0", result!.Descriptor.Version);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns null when no newer compatible versions exist.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_WithNoNewerVersions_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = CreateDescriptor("2.0.0")
            };
            var availableVersions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.0.0", minHost: new Version(1, 0, 0)),
                CreateAvailableExtension("1.5.0", minHost: new Version(1, 0, 0))
            };

            // Act
            var result = validator.GetCompatibleUpdate(installed, availableVersions);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate excludes incompatible newer versions.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_ExcludesIncompatibleNewerVersions()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = CreateDescriptor("1.0.0")
            };
            var availableVersions = new List<AvailableExtension>
            {
                CreateAvailableExtension("1.5.0", minHost: new Version(1, 0, 0)),  // Compatible
                CreateAvailableExtension("3.0.0", minHost: new Version(3, 0, 0))   // Incompatible (requires host 3.0)
            };

            // Act
            var result = validator.GetCompatibleUpdate(installed, availableVersions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1.5.0", result!.Descriptor.Version);
        }

        /// <summary>
        /// Tests that GetCompatibleUpdate returns null when installed version is invalid.
        /// </summary>
        [Fact]
        public void GetCompatibleUpdate_WithInvalidInstalledVersion_ReturnsNull()
        {
            // Arrange
            var validator = new CompatibilityValidator(new Version(2, 0, 0));
            var installed = new InstalledExtension
            {
                Descriptor = new ExtensionDescriptor
                {
                    Name = "test",
                    Version = "invalid-version"
                }
            };
            var availableVersions = new List<AvailableExtension>
            {
                CreateAvailableExtension("2.0.0", minHost: new Version(1, 0, 0))
            };

            // Act
            var result = validator.GetCompatibleUpdate(installed, availableVersions);

            // Assert
            Assert.Null(result);
        }
    }
}
