using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Metadata
{
    /// <summary>
    /// Contains test cases for the TargetPlatform enum.
    /// Tests the flag-based platform enumeration and bitwise operations.
    /// </summary>
    public class TargetPlatformTests
    {
        /// <summary>
        /// Tests that None platform has a value of 0.
        /// </summary>
        [Fact]
        public void None_ShouldHaveValueZero()
        {
            // Arrange & Act
            var none = TargetPlatform.None;

            // Assert
            Assert.Equal(0, (int)none);
        }

        /// <summary>
        /// Tests that Windows platform has a value of 1.
        /// </summary>
        [Fact]
        public void Windows_ShouldHaveValueOne()
        {
            // Arrange & Act
            var windows = TargetPlatform.Windows;

            // Assert
            Assert.Equal(1, (int)windows);
        }

        /// <summary>
        /// Tests that Linux platform has a value of 2.
        /// </summary>
        [Fact]
        public void Linux_ShouldHaveValueTwo()
        {
            // Arrange & Act
            var linux = TargetPlatform.Linux;

            // Assert
            Assert.Equal(2, (int)linux);
        }

        /// <summary>
        /// Tests that MacOS platform has a value of 4.
        /// </summary>
        [Fact]
        public void MacOS_ShouldHaveValueFour()
        {
            // Arrange & Act
            var macOS = TargetPlatform.MacOS;

            // Assert
            Assert.Equal(4, (int)macOS);
        }

        /// <summary>
        /// Tests that All platform is a combination of Windows, Linux, and MacOS.
        /// </summary>
        [Fact]
        public void All_ShouldBeCombinationOfAllPlatforms()
        {
            // Arrange
            var expected = TargetPlatform.Windows | TargetPlatform.Linux | TargetPlatform.MacOS;

            // Act
            var all = TargetPlatform.All;

            // Assert
            Assert.Equal(expected, all);
            Assert.Equal(7, (int)all); // 1 + 2 + 4 = 7
        }

        /// <summary>
        /// Tests that platform flags can be combined using bitwise OR.
        /// </summary>
        [Fact]
        public void PlatformFlags_ShouldSupportBitwiseOrCombination()
        {
            // Arrange & Act
            var combined = TargetPlatform.Windows | TargetPlatform.Linux;

            // Assert
            Assert.Equal(3, (int)combined); // 1 + 2 = 3
        }

        /// <summary>
        /// Tests that HasFlag correctly identifies if a platform is included in a combination.
        /// </summary>
        [Fact]
        public void HasFlag_ShouldReturnTrueForIncludedPlatform()
        {
            // Arrange
            var combined = TargetPlatform.Windows | TargetPlatform.Linux;

            // Act & Assert
            Assert.True(combined.HasFlag(TargetPlatform.Windows));
            Assert.True(combined.HasFlag(TargetPlatform.Linux));
            Assert.False(combined.HasFlag(TargetPlatform.MacOS));
        }

        /// <summary>
        /// Tests that All platform includes all individual platforms.
        /// </summary>
        [Fact]
        public void All_ShouldIncludeAllIndividualPlatforms()
        {
            // Arrange
            var all = TargetPlatform.All;

            // Act & Assert
            Assert.True(all.HasFlag(TargetPlatform.Windows));
            Assert.True(all.HasFlag(TargetPlatform.Linux));
            Assert.True(all.HasFlag(TargetPlatform.MacOS));
        }

        /// <summary>
        /// Tests that None platform does not include any other platform.
        /// </summary>
        [Fact]
        public void None_ShouldNotIncludeAnyPlatform()
        {
            // Arrange
            var none = TargetPlatform.None;

            // Act & Assert
            Assert.False(none.HasFlag(TargetPlatform.Windows));
            Assert.False(none.HasFlag(TargetPlatform.Linux));
            Assert.False(none.HasFlag(TargetPlatform.MacOS));
        }

        /// <summary>
        /// Tests that bitwise AND can be used to check platform overlap.
        /// </summary>
        [Fact]
        public void BitwiseAnd_ShouldIdentifyPlatformOverlap()
        {
            // Arrange
            var platform1 = TargetPlatform.Windows | TargetPlatform.Linux;
            var platform2 = TargetPlatform.Linux | TargetPlatform.MacOS;

            // Act
            var overlap = platform1 & platform2;

            // Assert
            Assert.Equal(TargetPlatform.Linux, overlap);
        }
    }
}
