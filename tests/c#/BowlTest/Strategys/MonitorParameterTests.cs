using GeneralUpdate.Bowl.Strategys;

namespace BowlTest.Strategys
{
    /// <summary>
    /// Contains test cases for the MonitorParameter class.
    /// Tests parameter initialization and property assignments.
    /// </summary>
    public class MonitorParameterTests
    {
        /// <summary>
        /// Tests that constructor creates a new instance with default values.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance_WithDefaultValues()
        {
            // Act
            var parameter = new MonitorParameter();

            // Assert
            Assert.NotNull(parameter);
            Assert.Equal("Upgrade", parameter.WorkModel);
        }

        /// <summary>
        /// Tests that WorkModel property has default value of "Upgrade".
        /// </summary>
        [Fact]
        public void WorkModel_HasDefaultValue_Upgrade()
        {
            // Arrange & Act
            var parameter = new MonitorParameter();

            // Assert
            Assert.Equal("Upgrade", parameter.WorkModel);
        }

        /// <summary>
        /// Tests that TargetPath property can be set and retrieved.
        /// </summary>
        [Fact]
        public void TargetPath_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedPath = "/path/to/target";

            // Act
            parameter.TargetPath = expectedPath;

            // Assert
            Assert.Equal(expectedPath, parameter.TargetPath);
        }

        /// <summary>
        /// Tests that FailDirectory property can be set and retrieved.
        /// </summary>
        [Fact]
        public void FailDirectory_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedPath = "/path/to/fail";

            // Act
            parameter.FailDirectory = expectedPath;

            // Assert
            Assert.Equal(expectedPath, parameter.FailDirectory);
        }

        /// <summary>
        /// Tests that BackupDirectory property can be set and retrieved.
        /// </summary>
        [Fact]
        public void BackupDirectory_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedPath = "/path/to/backup";

            // Act
            parameter.BackupDirectory = expectedPath;

            // Assert
            Assert.Equal(expectedPath, parameter.BackupDirectory);
        }

        /// <summary>
        /// Tests that ProcessNameOrId property can be set and retrieved.
        /// </summary>
        [Fact]
        public void ProcessNameOrId_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedValue = "myapp.exe";

            // Act
            parameter.ProcessNameOrId = expectedValue;

            // Assert
            Assert.Equal(expectedValue, parameter.ProcessNameOrId);
        }

        /// <summary>
        /// Tests that DumpFileName property can be set and retrieved.
        /// </summary>
        [Fact]
        public void DumpFileName_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedValue = "crash.dmp";

            // Act
            parameter.DumpFileName = expectedValue;

            // Assert
            Assert.Equal(expectedValue, parameter.DumpFileName);
        }

        /// <summary>
        /// Tests that FailFileName property can be set and retrieved.
        /// </summary>
        [Fact]
        public void FailFileName_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedValue = "fail.json";

            // Act
            parameter.FailFileName = expectedValue;

            // Assert
            Assert.Equal(expectedValue, parameter.FailFileName);
        }

        /// <summary>
        /// Tests that WorkModel property can be changed from default value.
        /// </summary>
        [Fact]
        public void WorkModel_CanBeChanged()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedValue = "Normal";

            // Act
            parameter.WorkModel = expectedValue;

            // Assert
            Assert.Equal(expectedValue, parameter.WorkModel);
        }

        /// <summary>
        /// Tests that ExtendedField property can be set and retrieved.
        /// </summary>
        [Fact]
        public void ExtendedField_CanBeSetAndRetrieved()
        {
            // Arrange
            var parameter = new MonitorParameter();
            var expectedValue = "1.0.0";

            // Act
            parameter.ExtendedField = expectedValue;

            // Assert
            Assert.Equal(expectedValue, parameter.ExtendedField);
        }

        /// <summary>
        /// Tests that all properties can be set together.
        /// </summary>
        [Fact]
        public void AllProperties_CanBeSetTogether()
        {
            // Arrange & Act
            var parameter = new MonitorParameter
            {
                TargetPath = "/target",
                FailDirectory = "/fail",
                BackupDirectory = "/backup",
                ProcessNameOrId = "app.exe",
                DumpFileName = "dump.dmp",
                FailFileName = "fail.json",
                WorkModel = "Normal",
                ExtendedField = "2.0.0"
            };

            // Assert
            Assert.Equal("/target", parameter.TargetPath);
            Assert.Equal("/fail", parameter.FailDirectory);
            Assert.Equal("/backup", parameter.BackupDirectory);
            Assert.Equal("app.exe", parameter.ProcessNameOrId);
            Assert.Equal("dump.dmp", parameter.DumpFileName);
            Assert.Equal("fail.json", parameter.FailFileName);
            Assert.Equal("Normal", parameter.WorkModel);
            Assert.Equal("2.0.0", parameter.ExtendedField);
        }
    }
}
