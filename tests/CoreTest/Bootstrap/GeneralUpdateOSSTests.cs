using GeneralUpdate.Core;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Contains test cases for the GeneralUpdateOSS class.
    /// Tests OSS update functionality for cross-platform scenarios.
    /// </summary>
    public class GeneralUpdateOSSTests
    {
        /// <summary>
        /// Tests that GeneralUpdateOSS is a sealed class.
        /// </summary>
        [Fact]
        public void Class_IsSealed()
        {
            // Arrange
            var type = typeof(GeneralUpdateOSS);

            // Assert
            Assert.True(type.IsSealed);
        }

        /// <summary>
        /// Tests that Start method exists and is static.
        /// </summary>
        [Fact]
        public void StartMethod_IsStatic()
        {
            // Arrange
            var type = typeof(GeneralUpdateOSS);
            var method = type.GetMethod("Start");

            // Assert
            Assert.NotNull(method);
            Assert.True(method.IsStatic);
        }
    }
}
