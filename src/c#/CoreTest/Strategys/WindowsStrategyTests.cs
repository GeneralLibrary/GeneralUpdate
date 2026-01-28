using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Strategys;
using Xunit;

namespace CoreTest.Strategys
{
    /// <summary>
    /// Contains test cases for the WindowsStrategy class.
    /// Tests Windows-specific update implementation.
    /// </summary>
    public class WindowsStrategyTests
    {
        /// <summary>
        /// Tests that WindowsStrategy can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var strategy = new WindowsStrategy();

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
            var strategy = new WindowsStrategy();
            var config = new GlobalConfigInfo
            {
                InstallPath = "/test/install",
                MainAppName = "TestApp.exe",
                TempPath = "/test/temp"
            };

            // Act & Assert - should not throw
            strategy.Create(config);
        }
    }
}
