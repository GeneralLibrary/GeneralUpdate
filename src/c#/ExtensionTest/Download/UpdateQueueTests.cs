using System;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Metadata;
using Xunit;

namespace ExtensionTest.Download
{
    /// <summary>
    /// Contains test cases for the UpdateQueue class.
    /// Tests thread-safe queue management, state transitions, and operation tracking.
    /// </summary>
    public class UpdateQueueTests
    {
        /// <summary>
        /// Helper method to create a sample AvailableExtension for testing.
        /// </summary>
        private AvailableExtension CreateTestExtension(string name = "test-extension")
        {
            return new AvailableExtension
            {
                Descriptor = new ExtensionDescriptor
                {
                    Name = name,
                    DisplayName = $"Test Extension {name}",
                    Version = "1.0.0"
                }
            };
        }

        /// <summary>
        /// Tests that Enqueue adds a new operation to the queue.
        /// </summary>
        [Fact]
        public void Enqueue_ShouldAddNewOperationToQueue()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();

            // Act
            var operation = queue.Enqueue(extension);

            // Assert
            Assert.NotNull(operation);
            Assert.Equal(UpdateState.Queued, operation.State);
            Assert.Same(extension, operation.Extension);
        }

        /// <summary>
        /// Tests that Enqueue throws ArgumentNullException when extension is null.
        /// </summary>
        [Fact]
        public void Enqueue_WithNullExtension_ThrowsArgumentNullException()
        {
            // Arrange
            var queue = new UpdateQueue();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
        }

        /// <summary>
        /// Tests that Enqueue prevents duplicate entries for the same extension.
        /// </summary>
        [Fact]
        public void Enqueue_WithDuplicateExtension_ReturnsExistingOperation()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension("duplicate-test");

            // Act
            var operation1 = queue.Enqueue(extension);
            var operation2 = queue.Enqueue(extension);

            // Assert
            Assert.Same(operation1, operation2);
            Assert.Single(queue.GetAllOperations());
        }

        /// <summary>
        /// Tests that Enqueue sets EnableRollback to true by default.
        /// </summary>
        [Fact]
        public void Enqueue_WithDefaultParameters_SetsEnableRollbackToTrue()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();

            // Act
            var operation = queue.Enqueue(extension);

            // Assert
            Assert.True(operation.EnableRollback);
        }

        /// <summary>
        /// Tests that Enqueue respects the enableRollback parameter.
        /// </summary>
        [Fact]
        public void Enqueue_WithEnableRollbackFalse_SetsPropertyToFalse()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();

            // Act
            var operation = queue.Enqueue(extension, enableRollback: false);

            // Assert
            Assert.False(operation.EnableRollback);
        }

        /// <summary>
        /// Tests that Enqueue sets the QueuedTime property.
        /// </summary>
        [Fact]
        public void Enqueue_ShouldSetQueuedTime()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var beforeEnqueue = DateTime.Now.AddSeconds(-1);

            // Act
            var operation = queue.Enqueue(extension);
            var afterEnqueue = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.True(operation.QueuedTime >= beforeEnqueue);
            Assert.True(operation.QueuedTime <= afterEnqueue);
        }

        /// <summary>
        /// Tests that GetNextQueued returns the first queued operation.
        /// </summary>
        [Fact]
        public void GetNextQueued_WithQueuedOperation_ReturnsFirstOperation()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var enqueuedOperation = queue.Enqueue(extension);

            // Act
            var nextOperation = queue.GetNextQueued();

            // Assert
            Assert.NotNull(nextOperation);
            Assert.Same(enqueuedOperation, nextOperation);
        }

        /// <summary>
        /// Tests that GetNextQueued returns null when queue is empty.
        /// </summary>
        [Fact]
        public void GetNextQueued_WithEmptyQueue_ReturnsNull()
        {
            // Arrange
            var queue = new UpdateQueue();

            // Act
            var nextOperation = queue.GetNextQueued();

            // Assert
            Assert.Null(nextOperation);
        }

        /// <summary>
        /// Tests that GetNextQueued skips non-queued operations.
        /// </summary>
        [Fact]
        public void GetNextQueued_SkipsNonQueuedOperations()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            
            var operation1 = queue.Enqueue(extension1);
            var operation2 = queue.Enqueue(extension2);
            
            queue.ChangeState(operation1.OperationId, UpdateState.Updating);

            // Act
            var nextOperation = queue.GetNextQueued();

            // Assert
            Assert.NotNull(nextOperation);
            Assert.Same(operation2, nextOperation);
        }

        /// <summary>
        /// Tests that ChangeState updates the operation state correctly.
        /// </summary>
        [Fact]
        public void ChangeState_ShouldUpdateOperationState()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.Updating);

            // Assert
            Assert.Equal(UpdateState.Updating, operation.State);
        }

        /// <summary>
        /// Tests that ChangeState sets StartTime when transitioning to Updating state.
        /// </summary>
        [Fact]
        public void ChangeState_ToUpdating_SetsStartTime()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);
            var beforeChange = DateTime.Now.AddSeconds(-1);

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.Updating);
            var afterChange = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.NotNull(operation.StartTime);
            Assert.True(operation.StartTime >= beforeChange);
            Assert.True(operation.StartTime <= afterChange);
        }

        /// <summary>
        /// Tests that ChangeState sets CompletionTime when transitioning to UpdateSuccessful.
        /// </summary>
        [Fact]
        public void ChangeState_ToUpdateSuccessful_SetsCompletionTime()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);
            var beforeChange = DateTime.Now.AddSeconds(-1);

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.UpdateSuccessful);
            var afterChange = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.NotNull(operation.CompletionTime);
            Assert.True(operation.CompletionTime >= beforeChange);
            Assert.True(operation.CompletionTime <= afterChange);
        }

        /// <summary>
        /// Tests that ChangeState sets CompletionTime when transitioning to UpdateFailed.
        /// </summary>
        [Fact]
        public void ChangeState_ToUpdateFailed_SetsCompletionTime()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, "Test error");

            // Assert
            Assert.NotNull(operation.CompletionTime);
            Assert.Equal("Test error", operation.ErrorMessage);
        }

        /// <summary>
        /// Tests that ChangeState sets CompletionTime when transitioning to Cancelled.
        /// </summary>
        [Fact]
        public void ChangeState_ToCancelled_SetsCompletionTime()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.Cancelled);

            // Assert
            Assert.NotNull(operation.CompletionTime);
        }

        /// <summary>
        /// Tests that ChangeState sets error message when provided.
        /// </summary>
        [Fact]
        public void ChangeState_WithErrorMessage_SetsErrorMessage()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);
            var errorMessage = "Download failed";

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.UpdateFailed, errorMessage);

            // Assert
            Assert.Equal(errorMessage, operation.ErrorMessage);
        }

        /// <summary>
        /// Tests that ChangeState with invalid operation ID does nothing.
        /// </summary>
        [Fact]
        public void ChangeState_WithInvalidOperationId_DoesNothing()
        {
            // Arrange
            var queue = new UpdateQueue();

            // Act
            queue.ChangeState("invalid-id", UpdateState.Updating);

            // Assert - no exception should be thrown
            Assert.Empty(queue.GetAllOperations());
        }

        /// <summary>
        /// Tests that UpdateProgress updates the progress percentage.
        /// </summary>
        [Fact]
        public void UpdateProgress_ShouldUpdateProgressPercentage()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.UpdateProgress(operation.OperationId, 45.5);

            // Assert
            Assert.Equal(45.5, operation.ProgressPercentage);
        }

        /// <summary>
        /// Tests that UpdateProgress clamps progress to 0 minimum.
        /// </summary>
        [Fact]
        public void UpdateProgress_WithNegativeValue_ClampsToZero()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.UpdateProgress(operation.OperationId, -10);

            // Assert
            Assert.Equal(0, operation.ProgressPercentage);
        }

        /// <summary>
        /// Tests that UpdateProgress clamps progress to 100 maximum.
        /// </summary>
        [Fact]
        public void UpdateProgress_WithValueOver100_ClampsTo100()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            queue.UpdateProgress(operation.OperationId, 150);

            // Assert
            Assert.Equal(100, operation.ProgressPercentage);
        }

        /// <summary>
        /// Tests that UpdateProgress with invalid operation ID does nothing.
        /// </summary>
        [Fact]
        public void UpdateProgress_WithInvalidOperationId_DoesNothing()
        {
            // Arrange
            var queue = new UpdateQueue();

            // Act
            queue.UpdateProgress("invalid-id", 50);

            // Assert - no exception should be thrown
            Assert.Empty(queue.GetAllOperations());
        }

        /// <summary>
        /// Tests that GetOperation returns the correct operation by ID.
        /// </summary>
        [Fact]
        public void GetOperation_WithValidId_ReturnsCorrectOperation()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);

            // Act
            var retrievedOperation = queue.GetOperation(operation.OperationId);

            // Assert
            Assert.NotNull(retrievedOperation);
            Assert.Same(operation, retrievedOperation);
        }

        /// <summary>
        /// Tests that GetOperation returns null for invalid ID.
        /// </summary>
        [Fact]
        public void GetOperation_WithInvalidId_ReturnsNull()
        {
            // Arrange
            var queue = new UpdateQueue();

            // Act
            var operation = queue.GetOperation("invalid-id");

            // Assert
            Assert.Null(operation);
        }

        /// <summary>
        /// Tests that GetAllOperations returns all operations.
        /// </summary>
        [Fact]
        public void GetAllOperations_ReturnsAllOperations()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            var extension3 = CreateTestExtension("ext3");
            
            queue.Enqueue(extension1);
            queue.Enqueue(extension2);
            queue.Enqueue(extension3);

            // Act
            var operations = queue.GetAllOperations();

            // Assert
            Assert.Equal(3, operations.Count);
        }

        /// <summary>
        /// Tests that GetAllOperations returns a defensive copy.
        /// </summary>
        [Fact]
        public void GetAllOperations_ReturnsDefensiveCopy()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            queue.Enqueue(extension);

            // Act
            var operations1 = queue.GetAllOperations();
            var operations2 = queue.GetAllOperations();

            // Assert
            Assert.NotSame(operations1, operations2);
        }

        /// <summary>
        /// Tests that GetOperationsByState filters operations correctly.
        /// </summary>
        [Fact]
        public void GetOperationsByState_FiltersCorrectly()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            var extension3 = CreateTestExtension("ext3");
            
            var op1 = queue.Enqueue(extension1);
            var op2 = queue.Enqueue(extension2);
            var op3 = queue.Enqueue(extension3);
            
            queue.ChangeState(op1.OperationId, UpdateState.Updating);
            queue.ChangeState(op2.OperationId, UpdateState.UpdateSuccessful);

            // Act
            var queuedOps = queue.GetOperationsByState(UpdateState.Queued);
            var updatingOps = queue.GetOperationsByState(UpdateState.Updating);
            var successfulOps = queue.GetOperationsByState(UpdateState.UpdateSuccessful);

            // Assert
            Assert.Single(queuedOps);
            Assert.Single(updatingOps);
            Assert.Single(successfulOps);
        }

        /// <summary>
        /// Tests that ClearCompleted removes completed operations.
        /// </summary>
        [Fact]
        public void ClearCompleted_RemovesCompletedOperations()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            var extension3 = CreateTestExtension("ext3");
            var extension4 = CreateTestExtension("ext4");
            
            var op1 = queue.Enqueue(extension1);
            var op2 = queue.Enqueue(extension2);
            var op3 = queue.Enqueue(extension3);
            var op4 = queue.Enqueue(extension4);
            
            queue.ChangeState(op1.OperationId, UpdateState.UpdateSuccessful);
            queue.ChangeState(op2.OperationId, UpdateState.UpdateFailed);
            queue.ChangeState(op3.OperationId, UpdateState.Cancelled);
            // op4 remains Queued

            // Act
            queue.ClearCompleted();

            // Assert
            var remaining = queue.GetAllOperations();
            Assert.Single(remaining);
            Assert.Equal(op4.OperationId, remaining[0].OperationId);
        }

        /// <summary>
        /// Tests that ClearCompleted does not remove queued or updating operations.
        /// </summary>
        [Fact]
        public void ClearCompleted_PreservesActiveOperations()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            
            var op1 = queue.Enqueue(extension1);
            var op2 = queue.Enqueue(extension2);
            
            queue.ChangeState(op2.OperationId, UpdateState.Updating);

            // Act
            queue.ClearCompleted();

            // Assert
            Assert.Equal(2, queue.GetAllOperations().Count);
        }

        /// <summary>
        /// Tests that RemoveOperation removes the specified operation.
        /// </summary>
        [Fact]
        public void RemoveOperation_RemovesSpecifiedOperation()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension1 = CreateTestExtension("ext1");
            var extension2 = CreateTestExtension("ext2");
            
            var op1 = queue.Enqueue(extension1);
            var op2 = queue.Enqueue(extension2);

            // Act
            queue.RemoveOperation(op1.OperationId);

            // Assert
            var remaining = queue.GetAllOperations();
            Assert.Single(remaining);
            Assert.Equal(op2.OperationId, remaining[0].OperationId);
        }

        /// <summary>
        /// Tests that RemoveOperation with invalid ID does nothing.
        /// </summary>
        [Fact]
        public void RemoveOperation_WithInvalidId_DoesNothing()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            queue.Enqueue(extension);

            // Act
            queue.RemoveOperation("invalid-id");

            // Assert
            Assert.Single(queue.GetAllOperations());
        }

        /// <summary>
        /// Tests that StateChanged event is raised when operation is enqueued.
        /// </summary>
        [Fact]
        public void Enqueue_RaisesStateChangedEvent()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var eventRaised = false;

            queue.StateChanged += (sender, args) => { eventRaised = true; };

            // Act
            queue.Enqueue(extension);

            // Assert
            Assert.True(eventRaised);
        }

        /// <summary>
        /// Tests that StateChanged event is raised when state changes.
        /// </summary>
        [Fact]
        public void ChangeState_RaisesStateChangedEvent()
        {
            // Arrange
            var queue = new UpdateQueue();
            var extension = CreateTestExtension();
            var operation = queue.Enqueue(extension);
            var eventRaised = false;

            queue.StateChanged += (sender, args) =>
            {
                if (args.PreviousState == UpdateState.Queued && args.CurrentState == UpdateState.Updating)
                {
                    eventRaised = true;
                }
            };

            // Act
            queue.ChangeState(operation.OperationId, UpdateState.Updating);

            // Assert
            Assert.True(eventRaised);
        }

        /// <summary>
        /// Tests thread safety of UpdateQueue by concurrent enqueue operations.
        /// </summary>
        [Fact]
        public void UpdateQueue_IsSafeForConcurrentAccess()
        {
            // Arrange
            var queue = new UpdateQueue();
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    var extension = CreateTestExtension($"ext{index}");
                    queue.Enqueue(extension);
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(10, queue.GetAllOperations().Count);
        }
    }
}
