using GeneralUpdate.Core.Driver;
using Xunit;

namespace CoreTest.Driver
{
    /// <summary>
    /// Contains test cases for the DriverInformation class.
    /// Tests driver information builder pattern and validation.
    /// </summary>
    public class DriverInformationTests
    {
        /// <summary>
        /// Tests that Builder can create a valid DriverInformation instance.
        /// </summary>
        [Fact]
        public void Builder_WithAllRequiredFields_BuildsSuccessfully()
        {
            // Arrange
            var builder = new DriverInformation.Builder();
            var fieldMappings = new Dictionary<string, string>
            {
                { "field1", "value1" }
            };

            // Act
            var information = builder
                .SetDriverFileExtension(".inf")
                .SetOutPutDirectory("/test/output")
                .SetDriverDirectory("/test/drivers")
                .SetFieldMappings(fieldMappings)
                .Build();

            // Assert
            Assert.NotNull(information);
            Assert.Equal(".inf", information.DriverFileExtension);
            Assert.Equal("/test/output", information.OutPutDirectory);
            Assert.Equal("/test/drivers", information.DriverDirectory);
            Assert.Equal(fieldMappings, information.FieldMappings);
        }

        /// <summary>
        /// Tests that Builder throws ArgumentNullException when OutPutDirectory is not set.
        /// </summary>
        [Fact]
        public void Builder_WithoutOutPutDirectory_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = new DriverInformation.Builder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                builder
                    .SetDriverFileExtension(".inf")
                    .SetDriverDirectory("/test/drivers")
                    .Build());
        }

        /// <summary>
        /// Tests that Builder throws ArgumentNullException when DriverFileExtension is not set.
        /// </summary>
        [Fact]
        public void Builder_WithoutDriverFileExtension_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = new DriverInformation.Builder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                builder
                    .SetOutPutDirectory("/test/output")
                    .SetDriverDirectory("/test/drivers")
                    .Build());
        }

        /// <summary>
        /// Tests that Builder can set and retrieve all properties.
        /// </summary>
        [Fact]
        public void Builder_SetAllProperties_ReturnsCorrectValues()
        {
            // Arrange
            var builder = new DriverInformation.Builder();
            var fieldMappings = new Dictionary<string, string>
            {
                { "key1", "val1" },
                { "key2", "val2" }
            };

            // Act
            var information = builder
                .SetDriverFileExtension(".sys")
                .SetOutPutDirectory("/output/path")
                .SetDriverDirectory("/driver/path")
                .SetFieldMappings(fieldMappings)
                .Build();

            // Assert
            Assert.Equal(".sys", information.DriverFileExtension);
            Assert.Equal("/output/path", information.OutPutDirectory);
            Assert.Equal("/driver/path", information.DriverDirectory);
            Assert.Equal(2, information.FieldMappings.Count);
        }

        /// <summary>
        /// Tests that Drivers property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Drivers_CanBeSetAndRetrieved()
        {
            // Arrange
            var builder = new DriverInformation.Builder();
            var drivers = new List<DriverInfo>
            {
                new DriverInfo { OriginalName = "driver1.inf", PublishedName = "oem1.inf" },
                new DriverInfo { OriginalName = "driver2.inf", PublishedName = "oem2.inf" }
            };

            // Act
            var information = builder
                .SetDriverFileExtension(".inf")
                .SetOutPutDirectory("/test/output")
                .SetDriverDirectory("/test/drivers")
                .Build();
            
            information.Drivers = drivers;

            // Assert
            Assert.NotNull(information.Drivers);
            Assert.Equal(2, information.Drivers.Count());
        }
    }
}
