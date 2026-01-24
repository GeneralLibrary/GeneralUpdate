using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Extension.Events;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Queue
{
    /// <summary>
    /// Manages the extension update queue.
    /// </summary>
    public class ExtensionUpdateQueue
    {
        private readonly List<ExtensionUpdateQueueItem> _queue = new List<ExtensionUpdateQueueItem>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Event fired when an update status changes.
        /// </summary>
        public event EventHandler<ExtensionUpdateStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// Adds an extension to the update queue.
        /// </summary>
        /// <param name="extension">The remote extension to update.</param>
        /// <param name="enableRollback">Whether to enable rollback on failure.</param>
        /// <returns>The queue item created.</returns>
        public ExtensionUpdateQueueItem Enqueue(RemoteExtension extension, bool enableRollback = true)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            lock (_lockObject)
            {
                // Check if the extension is already in the queue
                var existing = _queue.FirstOrDefault(item => 
                    item.Extension.Metadata.Id == extension.Metadata.Id &&
                    (item.Status == ExtensionUpdateStatus.Queued || item.Status == ExtensionUpdateStatus.Updating));

                if (existing != null)
                {
                    return existing;
                }

                var queueItem = new ExtensionUpdateQueueItem
                {
                    Extension = extension,
                    Status = ExtensionUpdateStatus.Queued,
                    EnableRollback = enableRollback,
                    QueuedTime = DateTime.Now
                };

                _queue.Add(queueItem);
                OnStatusChanged(queueItem, ExtensionUpdateStatus.Queued, ExtensionUpdateStatus.Queued);
                return queueItem;
            }
        }

        /// <summary>
        /// Gets the next queued item.
        /// </summary>
        /// <returns>The next queued item or null if the queue is empty.</returns>
        public ExtensionUpdateQueueItem? GetNextQueued()
        {
            lock (_lockObject)
            {
                return _queue.FirstOrDefault(item => item.Status == ExtensionUpdateStatus.Queued);
            }
        }

        /// <summary>
        /// Updates the status of a queue item.
        /// </summary>
        /// <param name="queueId">The queue item ID.</param>
        /// <param name="newStatus">The new status.</param>
        /// <param name="errorMessage">Optional error message if failed.</param>
        public void UpdateStatus(string queueId, ExtensionUpdateStatus newStatus, string? errorMessage = null)
        {
            lock (_lockObject)
            {
                var item = _queue.FirstOrDefault(q => q.QueueId == queueId);
                if (item == null)
                    return;

                var oldStatus = item.Status;
                item.Status = newStatus;
                item.ErrorMessage = errorMessage;

                if (newStatus == ExtensionUpdateStatus.Updating && item.StartTime == null)
                {
                    item.StartTime = DateTime.Now;
                }
                else if (newStatus == ExtensionUpdateStatus.UpdateSuccessful || 
                         newStatus == ExtensionUpdateStatus.UpdateFailed ||
                         newStatus == ExtensionUpdateStatus.Cancelled)
                {
                    item.EndTime = DateTime.Now;
                }

                OnStatusChanged(item, oldStatus, newStatus);
            }
        }

        /// <summary>
        /// Updates the progress of a queue item.
        /// </summary>
        /// <param name="queueId">The queue item ID.</param>
        /// <param name="progress">Progress percentage (0-100).</param>
        public void UpdateProgress(string queueId, double progress)
        {
            lock (_lockObject)
            {
                var item = _queue.FirstOrDefault(q => q.QueueId == queueId);
                if (item != null)
                {
                    item.Progress = Math.Max(0, Math.Min(100, progress));
                }
            }
        }

        /// <summary>
        /// Gets a queue item by ID.
        /// </summary>
        /// <param name="queueId">The queue item ID.</param>
        /// <returns>The queue item or null if not found.</returns>
        public ExtensionUpdateQueueItem? GetQueueItem(string queueId)
        {
            lock (_lockObject)
            {
                return _queue.FirstOrDefault(q => q.QueueId == queueId);
            }
        }

        /// <summary>
        /// Gets all items in the queue.
        /// </summary>
        /// <returns>List of all queue items.</returns>
        public List<ExtensionUpdateQueueItem> GetAllItems()
        {
            lock (_lockObject)
            {
                return new List<ExtensionUpdateQueueItem>(_queue);
            }
        }

        /// <summary>
        /// Gets all items with a specific status.
        /// </summary>
        /// <param name="status">The status to filter by.</param>
        /// <returns>List of queue items with the specified status.</returns>
        public List<ExtensionUpdateQueueItem> GetItemsByStatus(ExtensionUpdateStatus status)
        {
            lock (_lockObject)
            {
                return _queue.Where(item => item.Status == status).ToList();
            }
        }

        /// <summary>
        /// Removes completed or failed items from the queue.
        /// </summary>
        public void ClearCompletedItems()
        {
            lock (_lockObject)
            {
                _queue.RemoveAll(item => 
                    item.Status == ExtensionUpdateStatus.UpdateSuccessful || 
                    item.Status == ExtensionUpdateStatus.UpdateFailed ||
                    item.Status == ExtensionUpdateStatus.Cancelled);
            }
        }

        /// <summary>
        /// Removes a specific item from the queue.
        /// </summary>
        /// <param name="queueId">The queue item ID to remove.</param>
        public void RemoveItem(string queueId)
        {
            lock (_lockObject)
            {
                var item = _queue.FirstOrDefault(q => q.QueueId == queueId);
                if (item != null)
                {
                    _queue.Remove(item);
                }
            }
        }

        /// <summary>
        /// Raises the StatusChanged event.
        /// </summary>
        private void OnStatusChanged(ExtensionUpdateQueueItem item, ExtensionUpdateStatus oldStatus, ExtensionUpdateStatus newStatus)
        {
            StatusChanged?.Invoke(this, new ExtensionUpdateStatusChangedEventArgs
            {
                ExtensionId = item.Extension.Metadata.Id,
                ExtensionName = item.Extension.Metadata.Name,
                QueueItem = item,
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
        }
    }
}
