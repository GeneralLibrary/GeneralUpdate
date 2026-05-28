using System;
using System.Collections.Generic;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Event;
using IProgress = System.IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>;

namespace GeneralUpdate.Core.Download.Progress;

/// <summary>
/// 下载进度报告器，将 <see cref="IProgress{T}"/> 进度事件桥接到 <see cref="EventManager"/>
/// 事件系统，为传统事件监听器提供向后兼容的订阅方式。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IProgress{T}"/> 接口（其中 T 为 <c>DownloadProgress</c>），
/// 在报告下载进度时，同时触发以下事件：
/// </para>
/// <list type="bullet">
///   <item><term><c>ProgressEventArgs</c></term><description>每次报告进度时触发，包含下载百分比、已下载字节数等信息。</description></item>
///   <item><term><c>MultiDownloadCompletedEventArgs</c></term><description>当下载状态为 <c>Completed</c> 时触发。</description></item>
///   <item><term><c>MultiDownloadErrorEventArgs</c></term><description>当下载状态为 <c>Failed</c> 时触发。</description></item>
///   <item><term><c>MultiAllDownloadCompletedEventArgs</c></term><description>通过 <c>DispatchAllCompleted</c> 静态方法触发，表示所有下载任务已完成。</description></item>
/// </list>
/// <para>
/// 此类同时也支持直接注入 <c>onProgress</c> 和 <c>onCompleted</c> 回调委托，
/// 作为除 EventManager 之外的另一条通知通道。
/// </para>
/// </remarks>
public class DownloadProgressReporter : IProgress
{
    private readonly Action<Models.DownloadProgress>? _onProgress;
    private readonly Action? _onCompleted;

    /// <summary>
    /// 使用可选的进度回调和完成回调初始化进度报告器。
    /// </summary>
    /// <param name="onProgress">每次报告进度时调用的回调委托。</param>
    /// <param name="onCompleted">下载完成时调用的回调委托。</param>
    public DownloadProgressReporter(
        Action<Models.DownloadProgress>? onProgress = null,
        Action? onCompleted = null)
    {
        _onProgress = onProgress;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// 报告下载进度。触发进度回调、EventManager 事件，并根据下载状态触发完成或失败事件。
    /// </summary>
    /// <param name="value">包含当前下载进度信息的 <see cref="Models.DownloadProgress"/> 实例。</param>
    public void Report(Models.DownloadProgress value)
    {
        _onProgress?.Invoke(value);

        // Fire progress event via EventManager
        EventManager.Instance.Dispatch(this, new ProgressEventArgs(value));

        if (value.Status == Models.DownloadStatus.Completed)
        {
            _onCompleted?.Invoke();
            EventManager.Instance.Dispatch(this,
                new MultiDownloadCompletedEventArgs(value.AssetName ?? "unknown", true));
        }

        if (value.Status == Models.DownloadStatus.Failed)
        {
            EventManager.Instance.Dispatch(this,
                new MultiDownloadErrorEventArgs(new Exception("Download failed"), value.AssetName ?? "unknown"));
        }
    }

    /// <summary>
    /// 触发所有下载完成事件。此方法应在所有下载任务完成后调用一次，而不是每个资产完成后调用。
    /// 通常由下载编排器（Download Orchestrator）在全部下载完成后调用。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="success">所有下载是否全部成功。</param>
    /// <param name="details">每个下载资产的详情列表，包含资产对象和文件名。</param>
    public static void DispatchAllCompleted(object sender, bool success, List<(object, string)> details)
    {
        EventManager.Instance.Dispatch(sender,
            new MultiAllDownloadCompletedEventArgs(success, details ?? new List<(object, string)>()));
    }

    /// <summary>
    /// 创建一个将进度事件分发给 EventManager 的 <see cref="IProgress{T}"/> 实例。
    /// </summary>
    /// <returns>一个新的 <see cref="DownloadProgressReporter"/> 实例，用于桥接进度报告到事件系统。</returns>
    /// <remarks>
    /// 此工厂方法创建的报告器不包含自定义回调，仅通过 EventManager 分发事件。
    /// 适合只需订阅 EventManager 事件而不需要直接回调的场景。
    /// </remarks>
    public static IProgress CreateEventBridge()
        => new DownloadProgressReporter();
}
