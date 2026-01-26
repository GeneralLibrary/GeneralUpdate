using System;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.EventHandlers;
using Xunit;

namespace ExtensionTest.EventHandlers
{
    /// <summary>
    /// Contains test cases for extension event argument classes.
    /// Tests all event args types used in the extension system.
    /// </summary>
    public class ExtensionEventsTests
    {
        /// <summary>
        /// Tests that ExtensionEventArgs has default empty Name property.
        /// </summary>
        [Fact]
        public void ExtensionEventArgs_ShouldInitializeNameToEmptyString()
        {
            // Act
            var eventArgs = new ExtensionEventArgs();

            // Assert
            Assert.Equal(string.Empty, eventArgs.Name);
        }

        /// <summary>
        /// Tests that ExtensionEventArgs has default empty ExtensionName property.
        /// </summary>
        [Fact]
        public void ExtensionEventArgs_ShouldInitializeExtensionNameToEmptyString()
        {
            // Act
            var eventArgs = new ExtensionEventArgs();

            // Assert
            Assert.Equal(string.Empty, eventArgs.ExtensionName);
        }

        /// <summary>
        /// Tests that ExtensionEventArgs Name property can be set and retrieved.
        /// </summary>
        [Fact]
        public void ExtensionEventArgs_Name_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new ExtensionEventArgs();
            var expectedName = "test-extension";

            // Act
            eventArgs.Name = expectedName;

            // Assert
            Assert.Equal(expectedName, eventArgs.Name);
        }

        /// <summary>
        /// Tests that ExtensionEventArgs ExtensionName property can be set and retrieved.
        /// </summary>
        [Fact]
        public void ExtensionEventArgs_ExtensionName_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new ExtensionEventArgs();
            var expectedName = "Test Extension";

            // Act
            eventArgs.ExtensionName = expectedName;

            // Assert
            Assert.Equal(expectedName, eventArgs.ExtensionName);
        }

        /// <summary>
        /// Tests that ExtensionEventArgs inherits from EventArgs.
        /// </summary>
        [Fact]
        public void ExtensionEventArgs_ShouldInheritFromEventArgs()
        {
            // Act
            var eventArgs = new ExtensionEventArgs();

            // Assert
            Assert.IsAssignableFrom<EventArgs>(eventArgs);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs has Operation initialized.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_ShouldInitializeOperation()
        {
            // Act
            var eventArgs = new UpdateStateChangedEventArgs();

            // Assert
            Assert.NotNull(eventArgs.Operation);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs has default Queued state for PreviousState.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_ShouldHaveDefaultPreviousState()
        {
            // Act
            var eventArgs = new UpdateStateChangedEventArgs();

            // Assert
            Assert.Equal(UpdateState.Queued, eventArgs.PreviousState);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs has default Queued state for CurrentState.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_ShouldHaveDefaultCurrentState()
        {
            // Act
            var eventArgs = new UpdateStateChangedEventArgs();

            // Assert
            Assert.Equal(UpdateState.Queued, eventArgs.CurrentState);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs Operation property can be set and retrieved.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_Operation_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new UpdateStateChangedEventArgs();
            var operation = new UpdateOperation();

            // Act
            eventArgs.Operation = operation;

            // Assert
            Assert.Same(operation, eventArgs.Operation);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs state properties can be set.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_States_CanBeSet()
        {
            // Arrange
            var eventArgs = new UpdateStateChangedEventArgs();

            // Act
            eventArgs.PreviousState = UpdateState.Queued;
            eventArgs.CurrentState = UpdateState.Updating;

            // Assert
            Assert.Equal(UpdateState.Queued, eventArgs.PreviousState);
            Assert.Equal(UpdateState.Updating, eventArgs.CurrentState);
        }

        /// <summary>
        /// Tests that UpdateStateChangedEventArgs inherits from ExtensionEventArgs.
        /// </summary>
        [Fact]
        public void UpdateStateChangedEventArgs_ShouldInheritFromExtensionEventArgs()
        {
            // Act
            var eventArgs = new UpdateStateChangedEventArgs();

            // Assert
            Assert.IsAssignableFrom<ExtensionEventArgs>(eventArgs);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs has default zero ProgressPercentage.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldHaveDefaultProgressPercentage()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.Equal(0, eventArgs.ProgressPercentage);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs has default zero TotalBytes.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldHaveDefaultTotalBytes()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.Equal(0, eventArgs.TotalBytes);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs has default zero ReceivedBytes.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldHaveDefaultReceivedBytes()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.Equal(0, eventArgs.ReceivedBytes);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs has Speed initialized to null.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldHaveNullSpeed()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.Null(eventArgs.Speed);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs has default RemainingTime.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldHaveDefaultRemainingTime()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.Equal(TimeSpan.Zero, eventArgs.RemainingTime);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs properties can be set and retrieved.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new DownloadProgressEventArgs();
            var expectedProgress = 45.5;
            var expectedTotal = 1000000L;
            var expectedReceived = 455000L;
            var expectedSpeed = "1.5 MB/s";
            var expectedTime = TimeSpan.FromMinutes(5);

            // Act
            eventArgs.ProgressPercentage = expectedProgress;
            eventArgs.TotalBytes = expectedTotal;
            eventArgs.ReceivedBytes = expectedReceived;
            eventArgs.Speed = expectedSpeed;
            eventArgs.RemainingTime = expectedTime;

            // Assert
            Assert.Equal(expectedProgress, eventArgs.ProgressPercentage);
            Assert.Equal(expectedTotal, eventArgs.TotalBytes);
            Assert.Equal(expectedReceived, eventArgs.ReceivedBytes);
            Assert.Equal(expectedSpeed, eventArgs.Speed);
            Assert.Equal(expectedTime, eventArgs.RemainingTime);
        }

        /// <summary>
        /// Tests that DownloadProgressEventArgs inherits from ExtensionEventArgs.
        /// </summary>
        [Fact]
        public void DownloadProgressEventArgs_ShouldInheritFromExtensionEventArgs()
        {
            // Act
            var eventArgs = new DownloadProgressEventArgs();

            // Assert
            Assert.IsAssignableFrom<ExtensionEventArgs>(eventArgs);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs has default false Success.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_ShouldHaveDefaultSuccess()
        {
            // Act
            var eventArgs = new InstallationCompletedEventArgs();

            // Assert
            Assert.False(eventArgs.Success);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs has null InstallPath.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_ShouldHaveNullInstallPath()
        {
            // Act
            var eventArgs = new InstallationCompletedEventArgs();

            // Assert
            Assert.Null(eventArgs.InstallPath);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs has null ErrorMessage.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_ShouldHaveNullErrorMessage()
        {
            // Act
            var eventArgs = new InstallationCompletedEventArgs();

            // Assert
            Assert.Null(eventArgs.ErrorMessage);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs Success property can be set to true.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_Success_CanBeSetToTrue()
        {
            // Arrange
            var eventArgs = new InstallationCompletedEventArgs();

            // Act
            eventArgs.Success = true;

            // Assert
            Assert.True(eventArgs.Success);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs InstallPath can be set and retrieved.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_InstallPath_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new InstallationCompletedEventArgs();
            var expectedPath = "/path/to/extension";

            // Act
            eventArgs.InstallPath = expectedPath;

            // Assert
            Assert.Equal(expectedPath, eventArgs.InstallPath);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs ErrorMessage can be set and retrieved.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_ErrorMessage_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new InstallationCompletedEventArgs();
            var expectedMessage = "Installation failed";

            // Act
            eventArgs.ErrorMessage = expectedMessage;

            // Assert
            Assert.Equal(expectedMessage, eventArgs.ErrorMessage);
        }

        /// <summary>
        /// Tests that InstallationCompletedEventArgs inherits from ExtensionEventArgs.
        /// </summary>
        [Fact]
        public void InstallationCompletedEventArgs_ShouldInheritFromExtensionEventArgs()
        {
            // Act
            var eventArgs = new InstallationCompletedEventArgs();

            // Assert
            Assert.IsAssignableFrom<ExtensionEventArgs>(eventArgs);
        }

        /// <summary>
        /// Tests that RollbackCompletedEventArgs has default false Success.
        /// </summary>
        [Fact]
        public void RollbackCompletedEventArgs_ShouldHaveDefaultSuccess()
        {
            // Act
            var eventArgs = new RollbackCompletedEventArgs();

            // Assert
            Assert.False(eventArgs.Success);
        }

        /// <summary>
        /// Tests that RollbackCompletedEventArgs has null ErrorMessage.
        /// </summary>
        [Fact]
        public void RollbackCompletedEventArgs_ShouldHaveNullErrorMessage()
        {
            // Act
            var eventArgs = new RollbackCompletedEventArgs();

            // Assert
            Assert.Null(eventArgs.ErrorMessage);
        }

        /// <summary>
        /// Tests that RollbackCompletedEventArgs Success property can be set to true.
        /// </summary>
        [Fact]
        public void RollbackCompletedEventArgs_Success_CanBeSetToTrue()
        {
            // Arrange
            var eventArgs = new RollbackCompletedEventArgs();

            // Act
            eventArgs.Success = true;

            // Assert
            Assert.True(eventArgs.Success);
        }

        /// <summary>
        /// Tests that RollbackCompletedEventArgs ErrorMessage can be set and retrieved.
        /// </summary>
        [Fact]
        public void RollbackCompletedEventArgs_ErrorMessage_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new RollbackCompletedEventArgs();
            var expectedMessage = "Rollback failed";

            // Act
            eventArgs.ErrorMessage = expectedMessage;

            // Assert
            Assert.Equal(expectedMessage, eventArgs.ErrorMessage);
        }

        /// <summary>
        /// Tests that RollbackCompletedEventArgs inherits from ExtensionEventArgs.
        /// </summary>
        [Fact]
        public void RollbackCompletedEventArgs_ShouldInheritFromExtensionEventArgs()
        {
            // Act
            var eventArgs = new RollbackCompletedEventArgs();

            // Assert
            Assert.IsAssignableFrom<ExtensionEventArgs>(eventArgs);
        }

        /// <summary>
        /// Tests that all event args can be initialized using object initializers.
        /// </summary>
        [Fact]
        public void AllEventArgs_CanBeInitializedWithObjectInitializers()
        {
            // Act
            var updateStateArgs = new UpdateStateChangedEventArgs
            {
                Name = "test",
                ExtensionName = "Test Extension",
                Operation = new UpdateOperation(),
                PreviousState = UpdateState.Queued,
                CurrentState = UpdateState.Updating
            };

            var downloadProgressArgs = new DownloadProgressEventArgs
            {
                Name = "test",
                ProgressPercentage = 50,
                TotalBytes = 1000,
                ReceivedBytes = 500,
                Speed = "1 MB/s",
                RemainingTime = TimeSpan.FromMinutes(1)
            };

            var installationArgs = new InstallationCompletedEventArgs
            {
                Name = "test",
                Success = true,
                InstallPath = "/path",
                ErrorMessage = null
            };

            var rollbackArgs = new RollbackCompletedEventArgs
            {
                Name = "test",
                Success = true,
                ErrorMessage = null
            };

            // Assert
            Assert.NotNull(updateStateArgs);
            Assert.NotNull(downloadProgressArgs);
            Assert.NotNull(installationArgs);
            Assert.NotNull(rollbackArgs);
        }
    }
}
