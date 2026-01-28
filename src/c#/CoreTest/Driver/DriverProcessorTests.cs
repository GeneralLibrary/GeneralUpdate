using GeneralUpdate.Core.Driver;
using Moq;
using Xunit;

namespace CoreTest.Driver
{
    /// <summary>
    /// Contains test cases for the DriverProcessor class.
    /// Tests driver command management and execution.
    /// </summary>
    public class DriverProcessorTests
    {
        /// <summary>
        /// Tests that processor can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var processor = new DriverProcessor();

            // Assert
            Assert.NotNull(processor);
        }

        /// <summary>
        /// Tests that commands can be added to the processor.
        /// </summary>
        [Fact]
        public void AddCommand_AddsCommandToProcessor()
        {
            // Arrange
            var processor = new DriverProcessor();
            var mockCommand = new Mock<DriverCommand>();

            // Act
            processor.AddCommand(mockCommand.Object);

            // Assert - if no exception, command was added
        }

        /// <summary>
        /// Tests that ProcessCommands executes all added commands.
        /// </summary>
        [Fact]
        public void ProcessCommands_ExecutesAllCommands()
        {
            // Arrange
            var processor = new DriverProcessor();
            var mockCommand1 = new Mock<DriverCommand>();
            var mockCommand2 = new Mock<DriverCommand>();
            var mockCommand3 = new Mock<DriverCommand>();

            processor.AddCommand(mockCommand1.Object);
            processor.AddCommand(mockCommand2.Object);
            processor.AddCommand(mockCommand3.Object);

            // Act
            processor.ProcessCommands();

            // Assert
            mockCommand1.Verify(c => c.Execute(), Times.Once);
            mockCommand2.Verify(c => c.Execute(), Times.Once);
            mockCommand3.Verify(c => c.Execute(), Times.Once);
        }

        /// <summary>
        /// Tests that ProcessCommands with no commands does not throw.
        /// </summary>
        [Fact]
        public void ProcessCommands_WithNoCommands_DoesNotThrow()
        {
            // Arrange
            var processor = new DriverProcessor();

            // Act & Assert - should not throw
            processor.ProcessCommands();
            Assert.True(true);
        }

        /// <summary>
        /// Tests that ProcessCommands clears commands after execution.
        /// </summary>
        [Fact]
        public void ProcessCommands_ClearsCommandsAfterExecution()
        {
            // Arrange
            var processor = new DriverProcessor();
            var mockCommand = new Mock<DriverCommand>();
            processor.AddCommand(mockCommand.Object);

            // Act
            processor.ProcessCommands();
            processor.ProcessCommands(); // Call again

            // Assert - Execute should only be called once (from first ProcessCommands)
            mockCommand.Verify(c => c.Execute(), Times.Once);
        }
    }
}
