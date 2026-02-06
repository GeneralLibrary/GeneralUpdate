using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Extension.Download
{
    /// <summary>
    /// Manages a thread-safe queue of extension update operations.
    /// Tracks operation state and progress throughout the update lifecycle.
    /// </summary>
    public class UpdateQueue : IUpdateQueue
    {
        private readonly List<UpdateOperation> _operations = new List<UpdateOperation>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Occurs when an update operation changes state.
        /// </summary>
        public event EventHandler<EventHandlers.UpdateStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Adds a new extension update to the queue for processing.
        /// Prevents duplicate entries for extensions already queued or updating.
        /// </summary>
        /// <param name="extension">The extension to update.</param>
        /// <param name="enableRollback">Whether to enable automatic rollback on installation failure.</param>
        /// <returns>The created or existing update operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is null.</exception>
        public UpdateOperation Enqueue(Metadata.ExtensionMetadata extension, bool enableRollback = true)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            lock (_lockObject)
            {
                // Check if the extension is already queued or updating
                var existing = _operations.FirstOrDefault(op =>
                    op.Extension.Name == extension.Name &&
                    (op.State == UpdateState.Queued || op.State == UpdateState.Updating));

                if (existing != null)
                {
                    return existing;
                }

                var operation = new UpdateOperation
                {
                    Extension = extension,
                    State = UpdateState.Queued,
                    EnableRollback = enableRollback,
                    QueuedTime = DateTime.Now
                };

                _operations.Add(operation);
                OnStateChanged(operation, UpdateState.Queued, UpdateState.Queued);
                return operation;
            }
        }

        /// <summary>
        /// Retrieves the next update operation that is ready to be processed.
        /// </summary>
        /// <returns>The next queued operation if available; otherwise, null.</returns>
        public UpdateOperation? GetNextQueued()
        {
            lock (_lockObject)
            {
                return _operations.FirstOrDefault(op => op.State == UpdateState.Queued);
            }
        }

        /// <summary>
        /// Updates the state of a specific update operation.
        /// Automatically sets timestamps for state transitions.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation to update.</param>
        /// <param name="newState">The new state to set.</param>
        /// <param name="errorMessage">Optional error message if the operation failed.</param>
        public void ChangeState(string operationId, UpdateState newState, string? errorMessage = null)
        {
            lock (_lockObject)
            {
                var operation = _operations.FirstOrDefault(op => op.OperationId == operationId);
                if (operation == null)
                    return;

                var previousState = operation.State;
                operation.State = newState;
                operation.ErrorMessage = errorMessage;

                // Update timestamps based on state
                if (newState == UpdateState.Updating && operation.StartTime == null)
                {
                    operation.StartTime = DateTime.Now;
                }
                else if (newState == UpdateState.UpdateSuccessful ||
                         newState == UpdateState.UpdateFailed ||
                         newState == UpdateState.Cancelled)
                {
                    operation.CompletionTime = DateTime.Now;
                }

                OnStateChanged(operation, previousState, newState);
            }
        }

        /// <summary>
        /// Updates the download progress percentage for an operation.
        /// Progress is automatically clamped to the 0-100 range.
        /// </summary>
        /// <param name="operationId">The operation identifier.</param>
        /// <param name="progressPercentage">Progress percentage (0-100).</param>
        public void UpdateProgress(string operationId, double progressPercentage)
        {
            lock (_lockObject)
            {
                var operation = _operations.FirstOrDefault(op => op.OperationId == operationId);
                if (operation != null)
                {
                    operation.ProgressPercentage = Math.Max(0, Math.Min(100, progressPercentage));
                }
            }
        }

        /// <summary>
        /// Retrieves a specific update operation by its unique identifier.
        /// </summary>
        /// <param name="operationId">The operation identifier to search for.</param>
        /// <returns>The matching operation if found; otherwise, null.</returns>
        public UpdateOperation? GetOperation(string operationId)
        {
            lock (_lockObject)
            {
                return _operations.FirstOrDefault(op => op.OperationId == operationId);
            }
        }

        /// <summary>
        /// Gets all update operations currently in the queue.
        /// </summary>
        /// <returns>A defensive copy of the operations list.</returns>
        public List<UpdateOperation> GetAllOperations()
        {
            lock (_lockObject)
            {
                return new List<UpdateOperation>(_operations);
            }
        }

        /// <summary>
        /// Gets all operations that are currently in a specific state.
        /// </summary>
        /// <param name="state">The state to filter by.</param>
        /// <returns>A list of operations matching the specified state.</returns>
        public List<UpdateOperation> GetOperationsByState(UpdateState state)
        {
            lock (_lockObject)
            {
                return _operations.Where(op => op.State == state).ToList();
            }
        }

        /// <summary>
        /// Removes all completed or failed operations from the queue.
        /// This helps prevent memory accumulation in long-running applications.
        /// </summary>
        public void ClearCompleted()
        {
            lock (_lockObject)
            {
                _operations.RemoveAll(op =>
                    op.State == UpdateState.UpdateSuccessful ||
                    op.State == UpdateState.UpdateFailed ||
                    op.State == UpdateState.Cancelled);
            }
        }

        /// <summary>
        /// Removes a specific operation from the queue by its identifier.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation to remove.</param>
        public void RemoveOperation(string operationId)
        {
            lock (_lockObject)
            {
                var operation = _operations.FirstOrDefault(op => op.OperationId == operationId);
                if (operation != null)
                {
                    _operations.Remove(operation);
                }
            }
        }

        /// <summary>
        /// Raises the StateChanged event when an operation's state transitions.
        /// </summary>
        /// <param name="operation">The operation that changed state.</param>
        /// <param name="previousState">The state before the change.</param>
        /// <param name="currentState">The state after the change.</param>
        private void OnStateChanged(UpdateOperation operation, UpdateState previousState, UpdateState currentState)
        {
            StateChanged?.Invoke(this, new EventHandlers.UpdateStateChangedEventArgs
            {
                Name = operation.Extension.Name,
                ExtensionName = operation.Extension.DisplayName,
                Operation = operation,
                PreviousState = previousState,
                CurrentState = currentState
            });
        }
    }
}
