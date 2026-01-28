using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Strategys;
using Xunit;

namespace CoreTest.Strategys
{
    /// <summary>
    /// Contains test cases for the OSSStrategy class.
    /// Tests OSS (Object Storage Service) update implementation.
    /// </summary>
    public class OSSStrategyTests
    {
        /// <summary>
        /// Tests that OSSStrategy can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var strategy = new OSSStrategy();

            // Assert
            Assert.NotNull(strategy);
        }

        /// <summary>
        /// Tests that Create method accepts GlobalConfigInfoOSS.
        /// </summary>
        [Fact]
        public void Create_WithValidConfig_DoesNotThrow()
        {
            // Arrange
            var strategy = new OSSStrategy();
            var config = new GlobalConfigInfoOSS
            {
                AppName = "TestApp.exe",
                VersionFileName = "versions.json",
                Encoding = "utf-8"
            };

            // Act & Assert - should not throw
            strategy.Create(config);
        }
    }
}
