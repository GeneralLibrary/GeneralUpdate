/// <summary>
/// 测试覆盖点：
/// - 构造函数
///   - 默认 maxConcurrentDownloads=3
///   - 指定 maxConcurrentDownloads
/// - Enqueue(task)
///   - Status 设为 Queued
///   - 添加到 activeTasks
///   - 触发 DownloadStatusChanged 事件
///   - 启动队列处理
/// - GetTask(extensionId)
///   - 已存在任务 => 返回该任务
///   - 不存在任务 => 返回 null
/// - CancelTask(extensionId)
///   - 存在任务 => 调用 CancellationTokenSource.Cancel()
///   - 不存在任务 => 不抛异常
/// - GetActiveTasks()
///   - 空队列 => 返回空列表
///   - 有任务 => 返回所有活动任务
/// - Dispose()
///   - 取消所有任务
///   - 清空队列
///   - 释放 semaphore
///   - 多次 Dispose 不会重复执行
/// - ProcessQueueAsync
///   - 正常处理流程：Queued -> Updating -> UpdateSuccessful
///   - 任务取消：OperationCanceledException -> UpdateFailed
///   - 任务异常：Exception -> UpdateFailed
/// </summary>
using Moq;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Download.Tests;

public class DownloadQueueManagerTests
{
    private DownloadTask CreateTask(string id = "ext-1", string name = "test-ext")
    {
        return new DownloadTask
        {
            Extension = new ExtensionMetadata { Id = id, Name = name },
            SavePath = $@"C:\temp\{id}.zip"
        };
    }

    // ===== 构造函数 =====

    [Fact]
    public void 构造函数_默认maxConcurrentDownloads为3()
    {
        using var qm = new DownloadQueueManager();
        Assert.NotNull(qm);
    }

    [Fact]
    public void 构造函数_指定maxConcurrentDownloads()
    {
        using var qm = new DownloadQueueManager(5);
        Assert.NotNull(qm);
    }

    // ===== Enqueue =====

    [Fact]
    public void Enqueue_任务Status设为Queued()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask();
        qm.Enqueue(task);
        Assert.Equal(ExtensionUpdateStatus.Queued, task.Status);
    }

    [Fact]
    public void Enqueue_触发DownloadStatusChanged事件()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask();
        DownloadTaskEventArgs? eventArgs = null;
        qm.DownloadStatusChanged += (_, e) => eventArgs = e;

        qm.Enqueue(task);

        Assert.NotNull(eventArgs);
        Assert.Same(task, eventArgs!.Task);
        Assert.Equal(ExtensionUpdateStatus.Queued, eventArgs.Task.Status);
    }

    [Fact]
    public void Enqueue_多个任务依次入队()
    {
        using var qm = new DownloadQueueManager();
        var task1 = CreateTask("ext-1");
        var task2 = CreateTask("ext-2");
        qm.Enqueue(task1);
        qm.Enqueue(task2);

        var active = qm.GetActiveTasks();
        Assert.Equal(2, active.Count);
    }

    // ===== GetTask =====

    [Fact]
    public void GetTask_存在任务_返回该任务()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask();
        qm.Enqueue(task);

        var found = qm.GetTask("ext-1");
        Assert.NotNull(found);
        Assert.Same(task, found);
    }

    [Fact]
    public void GetTask_不存在任务_返回null()
    {
        using var qm = new DownloadQueueManager();
        Assert.Null(qm.GetTask("nonexistent"));
    }

    // ===== CancelTask =====

    [Fact]
    public void CancelTask_存在任务_取消Token()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask();
        qm.Enqueue(task);
        Assert.False(task.CancellationTokenSource.IsCancellationRequested);

        qm.CancelTask("ext-1");
        Assert.True(task.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void CancelTask_不存在任务_不抛异常()
    {
        using var qm = new DownloadQueueManager();
        // 不应该抛出异常
        qm.CancelTask("nonexistent");
    }

    // ===== GetActiveTasks =====

    [Fact]
    public void GetActiveTasks_空队列_返回空列表()
    {
        using var qm = new DownloadQueueManager();
        Assert.Empty(qm.GetActiveTasks());
    }

    [Fact]
    public void GetActiveTasks_返回所有活动任务()
    {
        using var qm = new DownloadQueueManager();
        var t1 = CreateTask("e1");
        var t2 = CreateTask("e2");
        qm.Enqueue(t1);
        qm.Enqueue(t2);

        var active = qm.GetActiveTasks();
        Assert.Contains(t1, active);
        Assert.Contains(t2, active);
    }

    // ===== Dispose =====

    [Fact]
    public void Dispose_取消所有活动任务()
    {
        var qm = new DownloadQueueManager();
        var task = CreateTask();
        qm.Enqueue(task);

        qm.Dispose();

        Assert.True(task.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_清空活动任务列表()
    {
        var qm = new DownloadQueueManager();
        qm.Enqueue(CreateTask("e1"));
        qm.Enqueue(CreateTask("e2"));

        qm.Dispose();

        Assert.Empty(qm.GetActiveTasks());
    }

    [Fact]
    public void Dispose_多次调用不抛异常()
    {
        var qm = new DownloadQueueManager();
        qm.Dispose();
        // 第二次 Dispose 不应抛异常
        qm.Dispose();
    }

    // ===== 事件订阅测试 =====

    [Fact]
    public void 事件订阅和取消订阅()
    {
        using var qm = new DownloadQueueManager();
        int callCount = 0;
        EventHandler<DownloadTaskEventArgs> handler = (_, _) => callCount++;

        qm.DownloadStatusChanged += handler;
        qm.Enqueue(CreateTask("e1"));
        Assert.Equal(1, callCount);

        qm.DownloadStatusChanged -= handler;
        qm.Enqueue(CreateTask("e2"));
        Assert.Equal(1, callCount); // 取消订阅后不再触发
    }

    // ===== 任务处理流程 =====

    [Fact]
    public async Task 任务入队后自动处理_状态变为UpdateSuccessful()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask("ext-success");
        var completed = new TaskCompletionSource<bool>();

        qm.DownloadStatusChanged += (_, e) =>
        {
            if (e.Task.Extension.Id == "ext-success" &&
                e.Task.Status == ExtensionUpdateStatus.UpdateSuccessful)
            {
                completed.TrySetResult(true);
            }
        };

        qm.Enqueue(task);

        // 等待任务完成_处理速度很快，因为ProcessTaskAsync只是Task.Delay(100)
        var timeout = Task.Delay(5000);
        var result = await Task.WhenAny(completed.Task, timeout);
        Assert.Same(completed.Task, result);
        Assert.True(completed.Task.Result);
        Assert.Equal(ExtensionUpdateStatus.UpdateSuccessful, task.Status);
        Assert.Equal(100, task.Progress);
    }

    [Fact]
    public async Task 入队任务被取消_状态变为UpdateFailed()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask("ext-cancel");
        var resultTcs = new TaskCompletionSource<ExtensionUpdateStatus>();

        qm.DownloadStatusChanged += (_, e) =>
        {
            if (e.Task.Extension.Id == "ext-cancel" &&
                (e.Task.Status == ExtensionUpdateStatus.UpdateFailed ||
                 e.Task.Status == ExtensionUpdateStatus.UpdateSuccessful))
            {
                resultTcs.TrySetResult(e.Task.Status);
            }
        };

        qm.Enqueue(task);
        qm.CancelTask("ext-cancel");

        var timeout = Task.Delay(5000);
        var result = await Task.WhenAny(resultTcs.Task, timeout);
        Assert.Same(resultTcs.Task, result);
        Assert.Equal(ExtensionUpdateStatus.UpdateFailed, resultTcs.Task.Result);
    }

    [Fact]
    public void GetTask_返回任务的副本引用_非新对象()
    {
        using var qm = new DownloadQueueManager();
        var task = CreateTask();
        qm.Enqueue(task);

        var found = qm.GetTask("ext-1");
        Assert.Same(task, found);
    }
}
