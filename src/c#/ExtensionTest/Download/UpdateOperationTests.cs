using System;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Download
{
    /// <summary>
    /// Contains test cases for the UpdateOperation class.
    /// Tests the update operation model including properties, initialization, and state tracking.
    /// </summary>
    public class UpdateOperationTests
    {
        /// <summary>
        /// Tests that a new UpdateOperation has a valid OperationId assigned.
        /// </summary>
        [Fact]
        public void Constructor_ShouldGenerateValidOperationId()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.NotNull(operation.OperationId);
            Assert.NotEmpty(operation.OperationId);
            Assert.True(Guid.TryParse(operation.OperationId, out _));
        }

        /// <summary>
        /// Tests that a new UpdateOperation has a non-null Extension property.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeExtension()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.NotNull(operation.Extension);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has default state of Queued.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetDefaultStateToQueued()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.Equal(UpdateState.Queued, operation.State);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has ProgressPercentage initialized to 0.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetProgressPercentageToZero()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.Equal(0, operation.ProgressPercentage);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has QueuedTime set to a recent value.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetQueuedTimeToNow()
        {
            // Arrange
            var beforeCreation = DateTime.Now.AddSeconds(-1);

            // Act
            var operation = new UpdateOperation();
            var afterCreation = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.True(operation.QueuedTime >= beforeCreation);
            Assert.True(operation.QueuedTime <= afterCreation);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has StartTime initially null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetStartTimeToNull()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.Null(operation.StartTime);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has CompletionTime initially null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetCompletionTimeToNull()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.Null(operation.CompletionTime);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has ErrorMessage initially null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetErrorMessageToNull()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.Null(operation.ErrorMessage);
        }

        /// <summary>
        /// Tests that a new UpdateOperation has EnableRollback set to true by default.
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetEnableRollbackToTrue()
        {
            // Act
            var operation = new UpdateOperation();

            // Assert
            Assert.True(operation.EnableRollback);
        }

        /// <summary>
        /// Tests that OperationId can be set to a custom value.
        /// </summary>
        [Fact]
        public void OperationId_CanBeSetToCustomValue()
        {
            // Arrange
            var operation = new UpdateOperation();
            var customId = "custom-operation-id";

            // Act
            operation.OperationId = customId;

            // Assert
            Assert.Equal(customId, operation.OperationId);
        }

        /// <summary>
        /// Tests that Extension property can be set and retrieved.
        /// </summary>
        [Fact]
        public void Extension_CanBeSetAndRetrieved()
        {
            // Arrange
            var operation = new UpdateOperation();
            var extension = new AvailableExtension();

            // Act
            operation.Extension = extension;

            // Assert
            Assert.Same(extension, operation.Extension);
        }

        /// <summary>
        /// Tests that State property can be changed to different values.
        /// </summary>
        [Fact]
        public void State_CanBeChangedToAllValidStates()
        {
            // Arrange
            var operation = new UpdateOperation();

            // Act & Assert
            operation.State = UpdateState.Queued;
            Assert.Equal(UpdateState.Queued, operation.State);

            operation.State = UpdateState.Updating;
            Assert.Equal(UpdateState.Updating, operation.State);

            operation.State = UpdateState.UpdateSuccessful;
            Assert.Equal(UpdateState.UpdateSuccessful, operation.State);

            operation.State = UpdateState.UpdateFailed;
            Assert.Equal(UpdateState.UpdateFailed, operation.State);

            operation.State = UpdateState.Cancelled;
            Assert.Equal(UpdateState.Cancelled, operation.State);
        }

        /// <summary>
        /// Tests that ProgressPercentage can be set to values between 0 and 100.
        /// </summary>
        [Fact]
        public void ProgressPercentage_CanBeSetToValidRange()
        {
            // Arrange
            var operation = new UpdateOperation();

            // Act & Assert
            operation.ProgressPercentage = 0;
            Assert.Equal(0, operation.ProgressPercentage);

            operation.ProgressPercentage = 50.5;
            Assert.Equal(50.5, operation.ProgressPercentage);

            operation.ProgressPercentage = 100;
            Assert.Equal(100, operation.ProgressPercentage);
        }

        /// <summary>
        /// Tests that StartTime can be set to a specific datetime value.
        /// </summary>
        [Fact]
        public void StartTime_CanBeSet()
        {
            // Arrange
            var operation = new UpdateOperation();
            var startTime = DateTime.Now;

            // Act
            operation.StartTime = startTime;

            // Assert
            Assert.Equal(startTime, operation.StartTime);
        }

        /// <summary>
        /// Tests that CompletionTime can be set to a specific datetime value.
        /// </summary>
        [Fact]
        public void CompletionTime_CanBeSet()
        {
            // Arrange
            var operation = new UpdateOperation();
            var completionTime = DateTime.Now;

            // Act
            operation.CompletionTime = completionTime;

            // Assert
            Assert.Equal(completionTime, operation.CompletionTime);
        }

        /// <summary>
        /// Tests that ErrorMessage can be set to a custom error string.
        /// </summary>
        [Fact]
        public void ErrorMessage_CanBeSet()
        {
            // Arrange
            var operation = new UpdateOperation();
            var errorMessage = "Download failed due to network error";

            // Act
            operation.ErrorMessage = errorMessage;

            // Assert
            Assert.Equal(errorMessage, operation.ErrorMessage);
        }

        /// <summary>
        /// Tests that EnableRollback can be changed from default true to false.
        /// </summary>
        [Fact]
        public void EnableRollback_CanBeSetToFalse()
        {
            // Arrange
            var operation = new UpdateOperation();

            // Act
            operation.EnableRollback = false;

            // Assert
            Assert.False(operation.EnableRollback);
        }

        /// <summary>
        /// Tests that each new UpdateOperation instance gets a unique OperationId.
        /// </summary>
        [Fact]
        public void Constructor_ShouldGenerateUniqueOperationIds()
        {
            // Act
            var operation1 = new UpdateOperation();
            var operation2 = new UpdateOperation();
            var operation3 = new UpdateOperation();

            // Assert
            Assert.NotEqual(operation1.OperationId, operation2.OperationId);
            Assert.NotEqual(operation2.OperationId, operation3.OperationId);
            Assert.NotEqual(operation1.OperationId, operation3.OperationId);
        }

        /// <summary>
        /// Tests that nullable properties can be set back to null after having a value.
        /// </summary>
        [Fact]
        public void NullableProperties_CanBeSetBackToNull()
        {
            // Arrange
            var operation = new UpdateOperation
            {
                StartTime = DateTime.Now,
                CompletionTime = DateTime.Now,
                ErrorMessage = "Some error"
            };

            // Act
            operation.StartTime = null;
            operation.CompletionTime = null;
            operation.ErrorMessage = null;

            // Assert
            Assert.Null(operation.StartTime);
            Assert.Null(operation.CompletionTime);
            Assert.Null(operation.ErrorMessage);
        }
    }
}
