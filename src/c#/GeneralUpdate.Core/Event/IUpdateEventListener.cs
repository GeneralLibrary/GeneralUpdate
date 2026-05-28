using GeneralUpdate.Core.Download;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// 更新事件监听器接口，定义了更新生命周期中所有事件类型的批量注册契约。
/// </summary>
/// <remarks>
/// <para>
/// 实现此接口并通过 <c>new GeneralUpdateBootstrap().AddEventListener&lt;MyListener&gt;()</c> 注册，
/// 即可接收更新流程中各个阶段的事件通知。
/// </para>
/// <para>
/// 事件类型覆盖完整的更新生命周期：
/// <list type="bullet">
///   <item><description><see cref="OnUpdateInfo"/>：发现可用更新版本信息时触发。</description></item>
///   <item><description><see cref="OnDownloadStatistics"/> / <see cref="OnProgress"/>：下载过程中的统计和进度信息。</description></item>
///   <item><description><see cref="OnDownloadCompleted"/> / <see cref="OnAllDownloadCompleted"/>：下载完成通知。</description></item>
///   <item><description><see cref="OnDownloadError"/>：下载出错时触发。</description></item>
///   <item><description><see cref="OnException"/>：更新流程中发生异常时触发。</description></item>
/// </list>
/// </para>
/// <para>
/// 如果只需要关注部分事件，建议继承 <see cref="UpdateEventListenerBase"/> 基类，
/// 仅重写需要处理的方法。
/// </para>
/// </remarks>
public interface IUpdateEventListener
{
    /// <summary>
    /// 所有下载任务全部完成时触发。
    /// </summary>
    /// <param name="args">包含所有下载完成状态的事件参数。</param>
    void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args);

    /// <summary>
    /// 单个下载任务完成时触发。
    /// </summary>
    /// <param name="args">包含单个下载完成状态的事件参数。</param>
    void OnDownloadCompleted(MultiDownloadCompletedEventArgs args);

    /// <summary>
    /// 下载过程中发生错误时触发。
    /// </summary>
    /// <param name="args">包含下载错误信息的事件参数。</param>
    void OnDownloadError(MultiDownloadErrorEventArgs args);

    /// <summary>
    /// 下载统计数据更新时触发。
    /// </summary>
    /// <param name="args">包含下载统计信息（速度、进度等）的事件参数。</param>
    void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args);

    /// <summary>
    /// 获取到更新版本信息时触发。
    /// </summary>
    /// <param name="args">包含更新版本信息的事件参数。</param>
    void OnUpdateInfo(UpdateInfoEventArgs args);

    /// <summary>
    /// 更新流程中发生异常时触发。
    /// </summary>
    /// <param name="args">包含异常信息的事件参数。</param>
    void OnException(ExceptionEventArgs args);

    /// <summary>
    /// 实时下载进度更新时触发。
    /// </summary>
    /// <param name="args">包含下载进度数据的事件参数。</param>
    void OnProgress(ProgressEventArgs args);
}

/// <summary>
/// <see cref="IUpdateEventListener"/> 的基类，提供所有事件方法的空实现。
/// 继承此类并仅重写需要处理的事件方法，避免实现接口时必须实现所有方法的负担。
/// </summary>
/// <remarks>
/// 使用示例：
/// <code>
/// public class MyListener : UpdateEventListenerBase
/// {
///     public override void OnProgress(ProgressEventArgs args)
///     {
///         Console.WriteLine($"进度: {args.Progress?.ProgressValue}%");
///     }
/// }
/// </code>
/// </remarks>
public abstract class UpdateEventListenerBase : IUpdateEventListener
{
    /// <summary>
    /// 所有下载任务完成时的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs args) { }

    /// <summary>
    /// 单个下载任务完成时的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnDownloadCompleted(MultiDownloadCompletedEventArgs args) { }

    /// <summary>
    /// 下载错误事件的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnDownloadError(MultiDownloadErrorEventArgs args) { }

    /// <summary>
    /// 下载统计信息更新的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnDownloadStatistics(MultiDownloadStatisticsEventArgs args) { }

    /// <summary>
    /// 更新信息可用时的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnUpdateInfo(UpdateInfoEventArgs args) { }

    /// <summary>
    /// 异常事件发生的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnException(ExceptionEventArgs args) { }

    /// <summary>
    /// 下载进度更新的空实现。重写此方法以处理该事件。
    /// </summary>
    /// <param name="args">事件参数。</param>
    public virtual void OnProgress(ProgressEventArgs args) { }
}
