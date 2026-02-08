using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Download;

namespace ExtensionTest;

public class DownloadQueueManagerTests : IDisposable
{
    private DownloadQueueManager? _manager;

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithDefaultConcurrency()
    {
        // Act
        _manager = new DownloadQueueManager();

        // Assert
        Assert.NotNull(_manager);
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithCustomConcurrency()
    {
        // Act
        _manager = new DownloadQueueManager(5);

        // Assert
        Assert.NotNull(_manager);
    }

    [Fact]
    public void Enqueue_ShouldAddTaskToQueue()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };

        // Act
        _manager.Enqueue(task);

        // Assert
        var retrievedTask = _manager.GetTask("ext1");
        Assert.NotNull(retrievedTask);
        Assert.Equal("ext1", retrievedTask.Extension.Id);
    }

    [Fact]
    public void Enqueue_ShouldSetStatusToQueued()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        var statusChanges = new List<ExtensionUpdateStatus>();
        
        _manager.DownloadStatusChanged += (sender, e) =>
        {
            statusChanges.Add(e.Task.Status);
        };

        // Act
        _manager.Enqueue(task);

        // Assert - Queued should be the first status
        Assert.Contains(ExtensionUpdateStatus.Queued, statusChanges);
    }

    [Fact]
    public void Enqueue_ShouldRaiseDownloadStatusChangedEvent()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        var eventRaised = false;

        _manager.DownloadStatusChanged += (sender, e) =>
        {
            eventRaised = true;
            Assert.Equal("ext1", e.Task.Extension.Id);
            Assert.Equal(ExtensionUpdateStatus.Queued, e.Task.Status);
        };

        // Act
        _manager.Enqueue(task);

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void GetTask_ShouldReturnTask_WhenExists()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        _manager.Enqueue(task);

        // Act
        var result = _manager.GetTask("ext1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ext1", result.Extension.Id);
    }

    [Fact]
    public void GetTask_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        _manager = new DownloadQueueManager();

        // Act
        var result = _manager.GetTask("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CancelTask_ShouldCancelExistingTask()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        _manager.Enqueue(task);

        // Act
        _manager.CancelTask("ext1");

        // Assert
        Assert.True(task.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void CancelTask_ShouldNotThrow_WhenTaskNotExists()
    {
        // Arrange
        _manager = new DownloadQueueManager();

        // Act & Assert
        var exception = Record.Exception(() => _manager.CancelTask("nonexistent"));
        Assert.Null(exception);
    }

    [Fact]
    public void GetActiveTasks_ShouldReturnEmptyList_WhenNoTasks()
    {
        // Arrange
        _manager = new DownloadQueueManager();

        // Act
        var tasks = _manager.GetActiveTasks();

        // Assert
        Assert.Empty(tasks);
    }

    [Fact]
    public void GetActiveTasks_ShouldReturnAllTasks()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var ext1 = CreateTestExtension("ext1");
        var ext2 = CreateTestExtension("ext2");
        var task1 = new DownloadTask { Extension = ext1 };
        var task2 = new DownloadTask { Extension = ext2 };

        _manager.Enqueue(task1);
        _manager.Enqueue(task2);

        // Act
        var tasks = _manager.GetActiveTasks();

        // Assert
        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, t => t.Extension.Id == "ext1");
        Assert.Contains(tasks, t => t.Extension.Id == "ext2");
    }

    [Fact]
    public async Task Enqueue_ShouldProcessTask_Eventually()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        var statusChanges = new List<ExtensionUpdateStatus>();

        _manager.DownloadStatusChanged += (sender, e) =>
        {
            statusChanges.Add(e.Task.Status);
        };

        // Act
        _manager.Enqueue(task);
        
        // Wait for processing (with timeout)
        await Task.Delay(300);

        // Assert
        Assert.Contains(ExtensionUpdateStatus.Queued, statusChanges);
        Assert.Contains(ExtensionUpdateStatus.Updating, statusChanges);
        Assert.Contains(ExtensionUpdateStatus.UpdateSuccessful, statusChanges);
    }

    [Fact]
    public async Task Enqueue_ShouldHandleCancellation()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var extension = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = extension };
        var statusChanges = new List<ExtensionUpdateStatus>();

        _manager.DownloadStatusChanged += (sender, e) =>
        {
            statusChanges.Add(e.Task.Status);
        };

        _manager.Enqueue(task);
        
        // Act - Cancel immediately after enqueueing
        _manager.CancelTask("ext1");
        
        // Wait for processing (with timeout)
        await Task.Delay(300);

        // Assert
        Assert.True(task.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public async Task Enqueue_MultipleTasksShouldProcess()
    {
        // Arrange
        _manager = new DownloadQueueManager(2); // Allow 2 concurrent downloads
        var completedTasks = 0;

        _manager.DownloadStatusChanged += (sender, e) =>
        {
            if (e.Task.Status == ExtensionUpdateStatus.UpdateSuccessful)
            {
                Interlocked.Increment(ref completedTasks);
            }
        };

        // Act - Enqueue multiple tasks
        for (int i = 1; i <= 5; i++)
        {
            var ext = CreateTestExtension($"ext{i}");
            var task = new DownloadTask { Extension = ext };
            _manager.Enqueue(task);
        }

        // Wait for all to complete (with timeout)
        await Task.Delay(1000);

        // Assert
        Assert.Equal(5, completedTasks);
    }

    [Fact]
    public void Dispose_ShouldCancelAllTasks()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var tasks = new List<DownloadTask>();

        for (int i = 1; i <= 3; i++)
        {
            var ext = CreateTestExtension($"ext{i}");
            var task = new DownloadTask { Extension = ext };
            tasks.Add(task);
            _manager.Enqueue(task);
        }

        // Act
        _manager.Dispose();

        // Assert
        foreach (var task in tasks)
        {
            Assert.True(task.CancellationTokenSource.IsCancellationRequested);
        }
    }

    [Fact]
    public void Dispose_ShouldClearActiveTasks()
    {
        // Arrange
        _manager = new DownloadQueueManager();
        var ext = CreateTestExtension("ext1");
        var task = new DownloadTask { Extension = ext };
        _manager.Enqueue(task);

        // Act
        _manager.Dispose();
        var activeTasks = _manager.GetActiveTasks();

        // Assert
        Assert.Empty(activeTasks);
    }

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        _manager = new DownloadQueueManager();

        // Act & Assert
        _manager.Dispose();
        var exception = Record.Exception(() => _manager.Dispose());
        Assert.Null(exception);
    }

    private ExtensionMetadata CreateTestExtension(string id)
    {
        return new ExtensionMetadata
        {
            Id = id,
            Name = $"Extension-{id}",
            Version = "1.0.0"
        };
    }
}
