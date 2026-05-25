/// <summary>
/// 测试覆盖点：
/// - 默认值：Extension 为 null, SavePath 为空字符串, Status 为默认枚举值
/// - Progress 默认值 0
/// - CancellationTokenSource 默认非 null
/// - 所有属性赋值和读取
/// - ErrorMessage 可为 null
/// - SavePath 可为空/长路径
/// </summary>
namespace GeneralUpdate.Extension.Common.Models.Tests;

public class DownloadTaskTests
{
    [Fact]
    public void 默认构造_Extension为null()
    {
        var task = new DownloadTask();
        Assert.Null(task.Extension);
    }

    [Fact]
    public void 默认构造_SavePath为空字符串()
    {
        var task = new DownloadTask();
        Assert.Equal(string.Empty, task.SavePath);
    }

    [Fact]
    public void 默认构造_Status为默认值Queued()
    {
        var task = new DownloadTask();
        Assert.Equal(ExtensionUpdateStatus.Queued, task.Status);
    }

    [Fact]
    public void 默认构造_Progress为0()
    {
        var task = new DownloadTask();
        Assert.Equal(0, task.Progress);
    }

    [Fact]
    public void 默认构造_CancellationTokenSource不为null()
    {
        var task = new DownloadTask();
        Assert.NotNull(task.CancellationTokenSource);
        Assert.False(task.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void ErrorMessage_默认为null()
    {
        var task = new DownloadTask();
        Assert.Null(task.ErrorMessage);
    }

    [Fact]
    public void 所有属性赋值后可正确读取()
    {
        var meta = new ExtensionMetadata { Id = "ext-1", Name = "test", Version = "1.0.0" };
        var task = new DownloadTask
        {
            Extension = meta,
            SavePath = @"C:\temp\ext.zip",
            Status = ExtensionUpdateStatus.Updating,
            Progress = 50,
            ErrorMessage = "Partial error"
        };
        Assert.Equal(meta, task.Extension);
        Assert.Equal(@"C:\temp\ext.zip", task.SavePath);
        Assert.Equal(ExtensionUpdateStatus.Updating, task.Status);
        Assert.Equal(50, task.Progress);
        Assert.Equal("Partial error", task.ErrorMessage);
    }

    [Fact]
    public void Status_切换为UpdateSuccessful()
    {
        var task = new DownloadTask { Status = ExtensionUpdateStatus.UpdateSuccessful };
        Assert.Equal(ExtensionUpdateStatus.UpdateSuccessful, task.Status);
    }

    [Fact]
    public void Status_切换为UpdateFailed()
    {
        var task = new DownloadTask { Status = ExtensionUpdateStatus.UpdateFailed };
        Assert.Equal(ExtensionUpdateStatus.UpdateFailed, task.Status);
    }

    [Fact]
    public void Progress_可设为100()
    {
        var task = new DownloadTask { Progress = 100 };
        Assert.Equal(100, task.Progress);
    }

    [Fact]
    public void Progress_可设为int最小值()
    {
        var task = new DownloadTask { Progress = int.MinValue };
        Assert.Equal(int.MinValue, task.Progress);
    }

    [Fact]
    public void CancellationTokenSource_可替换为新实例()
    {
        var task = new DownloadTask();
        var newCts = new CancellationTokenSource();
        task.CancellationTokenSource = newCts;
        Assert.Same(newCts, task.CancellationTokenSource);
    }

    [Fact]
    public void SavePath_可为长路径()
    {
        var longPath = @"C:\" + new string('x', 200) + @"\ext.zip";
        var task = new DownloadTask { SavePath = longPath };
        Assert.Equal(longPath, task.SavePath);
    }
}
