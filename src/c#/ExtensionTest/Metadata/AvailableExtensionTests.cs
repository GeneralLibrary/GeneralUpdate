using System;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Metadata
{
    /// <summary>
    /// Contains test cases for the AvailableExtension class.
    /// Tests the available extension model properties and initialization.
    /// </summary>
    public class AvailableExtensionTests
    {
        /// <summary>
        /// Tests that a new AvailableExtension has Descriptor initialized.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeDescriptor()
        {
            // Act
            var extension = new AvailableExtension();

            // Assert
            Assert.NotNull(extension.Descriptor);
        }

        /// <summary>
        /// Tests that a new AvailableExtension has IsPreRelease set to false by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetIsPreReleaseToFalse()
        {
            // Act
            var extension = new AvailableExtension();

            // Assert
            Assert.False(extension.IsPreRelease);
        }

        /// <summary>
        /// Tests that a new AvailableExtension has DownloadCount initialized to null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetDownloadCountToNull()
        {
            // Act
            var extension = new AvailableExtension();

            // Assert
            Assert.Null(extension.DownloadCount);
        }

        /// <summary>
        /// Tests that Descriptor property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Descriptor_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new AvailableExtension();
            var descriptor = new ExtensionDescriptor
            {
                Name = "test-extension",
                Version = "1.0.0"
            };

            // Act
            extension.Descriptor = descriptor;

            // Assert
            Assert.Same(descriptor, extension.Descriptor);
        }

        /// <summary>
        /// Tests that IsPreRelease property can be set to true.
        /// </summary>
        [Fact]
        public void IsPreRelease_CanBeSetToTrue()
        {
            // Arrange
            var extension = new AvailableExtension();

            // Act
            extension.IsPreRelease = true;

            // Assert
            Assert.True(extension.IsPreRelease);
        }

        /// <summary>
        /// Tests that DownloadCount property can be set and retrieved.
        /// </summary>
        [Fact]
        public void DownloadCount_CanBeSetAndRetrieved()
        {
            // Arrange
            var extension = new AvailableExtension();
            var expectedCount = 10000L;

            // Act
            extension.DownloadCount = expectedCount;

            // Assert
            Assert.Equal(expectedCount, extension.DownloadCount);
        }

        /// <summary>
        /// Tests that DownloadCount can be set to zero.
        /// </summary>
        [Fact]
        public void DownloadCount_CanBeSetToZero()
        {
            // Arrange
            var extension = new AvailableExtension();

            // Act
            extension.DownloadCount = 0;

            // Assert
            Assert.Equal(0, extension.DownloadCount);
        }

        /// <summary>
        /// Tests that DownloadCount can be set back to null after having a value.
        /// </summary>
        [Fact]
        public void DownloadCount_CanBeSetBackToNull()
        {
            // Arrange
            var extension = new AvailableExtension { DownloadCount = 1000 };

            // Act
            extension.DownloadCount = null;

            // Assert
            Assert.Null(extension.DownloadCount);
        }
    }
}
