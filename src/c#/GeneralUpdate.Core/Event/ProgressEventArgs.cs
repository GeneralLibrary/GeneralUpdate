using System;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Models;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// 进度事件参数，封装下载进度或差异补丁进度的快照信息。
/// </summary>
/// <remarks>
/// <para>
/// ProgressEventArgs 承载两种可能的进度数据类型：
/// <list type="bullet">
///   <item><description><see cref="DownloadProgress"/>：文件下载过程中的进度信息（下载速度、已完成字节数、总字节数等）。</description></item>
///   <item><description><see cref="DiffProgress"/>：差异补丁生成或应用过程中的进度信息。</description></item>
/// </list>
/// </para>
/// <para>
/// 事件接收方应检查 <see cref="Progress"/> 和 <see cref="DiffProgress"/> 哪个不为 <c>null</c>，
/// 以确定当前进度事件的类型。两个属性不会同时有值。
/// </para>
/// </remarks>
public class ProgressEventArgs : EventArgs
{
    /// <summary>
    /// 获取下载进度的快照对象。
    /// </summary>
    /// <value>如果当前事件是下载进度更新，则返回 <see cref="DownloadProgress"/> 实例；否则为 <c>null</c>。</value>
    public DownloadProgress? Progress { get; }

    /// <summary>
    /// 获取差异补丁进度的快照对象。
    /// </summary>
    /// <value>如果当前事件是差异补丁进度更新，则返回 <see cref="DiffProgress"/> 实例；否则为 <c>null</c>。</value>
    public DiffProgress? DiffProgress { get; }

    /// <summary>
    /// 使用下载进度数据初始化 <see cref="ProgressEventArgs"/> 的新实例。
    /// </summary>
    /// <param name="progress">下载进度快照。</param>
    public ProgressEventArgs(DownloadProgress progress)
    {
        Progress = progress;
    }

    /// <summary>
    /// 使用差异补丁进度数据初始化 <see cref="ProgressEventArgs"/> 的新实例。
    /// </summary>
    /// <param name="diffProgress">差异补丁进度快照。</param>
    public ProgressEventArgs(DiffProgress diffProgress)
    {
        DiffProgress = diffProgress;
    }
}
