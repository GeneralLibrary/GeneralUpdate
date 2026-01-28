using GeneralUpdate.Core.Driver;
using Xunit;

namespace CoreTest.Driver
{
    /// <summary>
    /// Contains test cases for the DriverInfo class.
    /// Tests driver metadata storage.
    /// </summary>
    public class DriverInfoTests
    {
        /// <summary>
        /// Tests that DriverInfo properties can be set and retrieved.
        /// </summary>
        [Fact]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var driverInfo = new DriverInfo();

            // Act
            driverInfo.PublishedName = "oem1.inf";
            driverInfo.OriginalName = "mydriver.inf";
            driverInfo.Provider = "Microsoft";
            driverInfo.ClassName = "Display";
            driverInfo.ClassGUID = "{4d36e968-e325-11ce-bfc1-08002be10318}";
            driverInfo.Version = "1.0.0.0";
            driverInfo.Signer = "Microsoft Windows Hardware Compatibility Publisher";

            // Assert
            Assert.Equal("oem1.inf", driverInfo.PublishedName);
            Assert.Equal("mydriver.inf", driverInfo.OriginalName);
            Assert.Equal("Microsoft", driverInfo.Provider);
            Assert.Equal("Display", driverInfo.ClassName);
            Assert.Equal("{4d36e968-e325-11ce-bfc1-08002be10318}", driverInfo.ClassGUID);
            Assert.Equal("1.0.0.0", driverInfo.Version);
            Assert.Equal("Microsoft Windows Hardware Compatibility Publisher", driverInfo.Signer);
        }

        /// <summary>
        /// Tests that DriverInfo can be instantiated with default values.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstanceWithDefaultValues()
        {
            // Act
            var driverInfo = new DriverInfo();

            // Assert
            Assert.NotNull(driverInfo);
            Assert.Null(driverInfo.PublishedName);
            Assert.Null(driverInfo.OriginalName);
            Assert.Null(driverInfo.Provider);
            Assert.Null(driverInfo.ClassName);
            Assert.Null(driverInfo.ClassGUID);
            Assert.Null(driverInfo.Version);
            Assert.Null(driverInfo.Signer);
        }
    }
}
