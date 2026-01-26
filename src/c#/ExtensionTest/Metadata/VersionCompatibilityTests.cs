using System;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Metadata
{
    /// <summary>
    /// Contains test cases for the VersionCompatibility class.
    /// Tests version range validation and compatibility checking logic.
    /// </summary>
    public class VersionCompatibilityTests
    {
        /// <summary>
        /// Tests that IsCompatibleWith returns true when no version constraints are set.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_NoConstraints_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility();
            var hostVersion = new Version(1, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns true when host version meets minimum requirement.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_MeetsMinimumVersion_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0)
            };
            var hostVersion = new Version(1, 5, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns false when host version is below minimum requirement.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_BelowMinimumVersion_ReturnsFalse()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(2, 0, 0)
            };
            var hostVersion = new Version(1, 5, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns true when host version is within maximum limit.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_WithinMaximumVersion_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MaxHostVersion = new Version(3, 0, 0)
            };
            var hostVersion = new Version(2, 5, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns false when host version exceeds maximum limit.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_ExceedsMaximumVersion_ReturnsFalse()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MaxHostVersion = new Version(2, 0, 0)
            };
            var hostVersion = new Version(3, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns true when host version is within both min and max constraints.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_WithinBothConstraints_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0),
                MaxHostVersion = new Version(3, 0, 0)
            };
            var hostVersion = new Version(2, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns false when host version violates minimum constraint in a range.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_BelowMinimumInRange_ReturnsFalse()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0),
                MaxHostVersion = new Version(3, 0, 0)
            };
            var hostVersion = new Version(0, 9, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns false when host version violates maximum constraint in a range.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_ExceedsMaximumInRange_ReturnsFalse()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0),
                MaxHostVersion = new Version(3, 0, 0)
            };
            var hostVersion = new Version(3, 1, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns true when host version exactly equals minimum version.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_ExactlyMinimumVersion_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(2, 0, 0)
            };
            var hostVersion = new Version(2, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith returns true when host version exactly equals maximum version.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_ExactlyMaximumVersion_ReturnsTrue()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MaxHostVersion = new Version(2, 0, 0)
            };
            var hostVersion = new Version(2, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsCompatibleWith throws ArgumentNullException when host version is null.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_NullHostVersion_ThrowsArgumentNullException()
        {
            // Arrange
            var compatibility = new VersionCompatibility();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => compatibility.IsCompatibleWith(null!));
        }

        /// <summary>
        /// Tests that MinHostVersion property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void MinHostVersion_SetAndGet_ReturnsCorrectValue()
        {
            // Arrange
            var compatibility = new VersionCompatibility();
            var expectedVersion = new Version(1, 2, 3);

            // Act
            compatibility.MinHostVersion = expectedVersion;

            // Assert
            Assert.Equal(expectedVersion, compatibility.MinHostVersion);
        }

        /// <summary>
        /// Tests that MaxHostVersion property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void MaxHostVersion_SetAndGet_ReturnsCorrectValue()
        {
            // Arrange
            var compatibility = new VersionCompatibility();
            var expectedVersion = new Version(4, 5, 6);

            // Act
            compatibility.MaxHostVersion = expectedVersion;

            // Assert
            Assert.Equal(expectedVersion, compatibility.MaxHostVersion);
        }

        /// <summary>
        /// Tests that both version constraints can be set to null (no constraints).
        /// </summary>
        [Fact]
        public void VersionConstraints_CanBeSetToNull()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 0, 0),
                MaxHostVersion = new Version(2, 0, 0)
            };

            // Act
            compatibility.MinHostVersion = null;
            compatibility.MaxHostVersion = null;

            // Assert
            Assert.Null(compatibility.MinHostVersion);
            Assert.Null(compatibility.MaxHostVersion);
        }

        /// <summary>
        /// Tests compatibility with a complex version number including build and revision.
        /// </summary>
        [Fact]
        public void IsCompatibleWith_ComplexVersionNumbers_WorksCorrectly()
        {
            // Arrange
            var compatibility = new VersionCompatibility
            {
                MinHostVersion = new Version(1, 2, 3, 4),
                MaxHostVersion = new Version(2, 3, 4, 5)
            };
            var hostVersion = new Version(1, 5, 0, 0);

            // Act
            var result = compatibility.IsCompatibleWith(hostVersion);

            // Assert
            Assert.True(result);
        }
    }
}
