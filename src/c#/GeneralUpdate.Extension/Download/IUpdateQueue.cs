using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.Download
{
    /// <summary>
    /// Defines the contract for managing the extension update queue.
    /// </summary>
    public interface IUpdateQueue
    {
        /// <summary>
        /// Occurs when an update operation changes state.
        /// </summary>
        event EventHandler<EventHandlers.UpdateStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Adds an extension update to the queue.
        /// </summary>
        /// <param name="extension">The extension to update.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The created update operation.</returns>
        UpdateOperation Enqueue(Metadata.ExtensionMetadata extension, bool enableRollback = true);

        /// <summary>
        /// Gets the next queued update operation.
        /// </summary>
        /// <returns>The next queued operation if available; otherwise, null.</returns>
        UpdateOperation? GetNextQueued();

        /// <summary>
        /// Updates the state of an update operation.
        /// </summary>
        /// <param name="operationId">The operation identifier.</param>
        /// <param name="newState">The new state.</param>
        /// <param name="errorMessage">Optional error message if failed.</param>
        void ChangeState(string operationId, UpdateState newState, string? errorMessage = null);

        /// <summary>
        /// Updates the progress of an update operation.
        /// </summary>
        /// <param name="operationId">The operation identifier.</param>
        /// <param name="progressPercentage">Progress percentage (0-100).</param>
        void UpdateProgress(string operationId, double progressPercentage);

        /// <summary>
        /// Gets an update operation by its identifier.
        /// </summary>
        /// <param name="operationId">The operation identifier.</param>
        /// <returns>The update operation if found; otherwise, null.</returns>
        UpdateOperation? GetOperation(string operationId);

        /// <summary>
        /// Gets all update operations in the queue.
        /// </summary>
        /// <returns>A list of all operations.</returns>
        List<UpdateOperation> GetAllOperations();

        /// <summary>
        /// Gets all operations with a specific state.
        /// </summary>
        /// <param name="state">The state to filter by.</param>
        /// <returns>A list of operations with the specified state.</returns>
        List<UpdateOperation> GetOperationsByState(UpdateState state);

        /// <summary>
        /// Removes completed or failed operations from the queue.
        /// </summary>
        void ClearCompleted();

        /// <summary>
        /// Removes a specific operation from the queue.
        /// </summary>
        /// <param name="operationId">The operation identifier to remove.</param>
        void RemoveOperation(string operationId);
    }
}
