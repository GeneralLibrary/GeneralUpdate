using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Strategys;
using Xunit;

namespace CoreTest.Strategys
{
    /// <summary>
    /// Contains test cases for the LinuxStrategy class.
    /// Tests Linux-specific update implementation.
    /// </summary>
    public class LinuxStrategyTests
    {
        /// <summary>
        /// Tests that LinuxStrategy can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var strategy = new LinuxStrategy();

            // Assert
            Assert.NotNull(strategy);
        }

        /// <summary>
        /// Tests that Create method accepts GlobalConfigInfo.
        /// </summary>
        [Fact]
        public void Create_WithValidConfig_DoesNotThrow()
        {
            // Arrange
            var strategy = new LinuxStrategy();
            var config = new GlobalConfigInfo
            {
                InstallPath = "/test/install",
                MainAppName = "TestApp",
                TempPath = "/test/temp"
            };

            // Act & Assert - should not throw
            strategy.Create(config);
        }
    }
}
