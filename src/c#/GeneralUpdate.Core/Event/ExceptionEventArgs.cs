using System;

namespace GeneralUpdate.Core.Event;

/// <summary>
/// 更新流程中的异常事件参数，封装异常对象和自定义错误消息。
/// </summary>
/// <remarks>
/// <para>
/// 当更新流程中的某个环节（如下载、解压、文件操作等）发生异常时，
/// 会通过 <see cref="EventManager"/> 分发此事件参数。
/// </para>
/// <para>
/// 事件接收方可以通过 <see cref="Exception"/> 属性获取详细的异常信息，
/// 通过 <see cref="Message"/> 属性获取可读的错误描述。
/// </para>
/// </remarks>
public class ExceptionEventArgs(Exception? exception = null, string? message = null) : EventArgs
{
    /// <summary>
    /// 获取与事件关联的异常对象。
    /// </summary>
    /// <value>可能为 <c>null</c>，如果异常信息仅通过文本消息传递。</value>
    public Exception Exception { get; private set; } = exception;

    /// <summary>
    /// 获取自定义的错误描述消息。
    /// </summary>
    /// <value>可能为 <c>null</c>，如果未提供自定义消息。</value>
    public string Message { get; private set; } = message;
}