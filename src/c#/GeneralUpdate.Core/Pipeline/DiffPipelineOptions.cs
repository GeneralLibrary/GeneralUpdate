using System;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// 配置 <see cref="DiffPipeline"/> 运行时的选项。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipelineOptions"/> 定义了差异管道的运行时行为参数。
/// 这些选项通过 <see cref="DiffPipelineBuilder"/> 的流畅 API 进行配置，
/// 或直接传递给 <see cref="DiffPipeline"/> 的构造函数。
/// </para>
/// <para>
/// 默认值：
/// <list type="bullet">
///   <item><description><see cref="MaxDegreeOfParallelism"/> = 2 — 同时处理 2 个文件。</description></item>
///   <item><description><see cref="StopOnFirstError"/> = <c>false</c> — 出现错误时继续处理其他文件。</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class DiffPipelineOptions
{
    /// <summary>
    /// 获取或设置可以同时处理的文件最大数量。
    /// </summary>
    /// <value>
    /// 最大并行文件处理数。默认值为 2。设置为 1 可强制串行处理。
    /// </value>
    /// <remarks>
    /// <para>
    /// 此属性控制 <see cref="DiffPipeline"/> 内部使用的 <see cref="System.Threading.SemaphoreSlim"/>
    /// 的初始计数，用于限制并发文件处理的任务数。
    /// </para>
    /// <para>
    /// 调优建议：
    /// <list type="bullet">
    ///   <item><description>1：完全串行执行，内存占用最低，适用于资源受限环境。</description></item>
    ///   <item><description>2（默认值）：最小并行度，在大多数环境下提供良好的性能提升。</description></item>
    ///   <item><description>4：适用于现代多核 CPU 和 SSD 存储。</description></item>
    ///   <item><description>8 以上：适用于高性能服务器，需注意 I/O 饱和。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = 2;

    /// <summary>
    /// 获取或设置一个值，指示在文件处理出错时是否立即停止整个管道。
    /// </summary>
    /// <value>
    /// 如果为 <c>true</c>，第一个文件错误会导致管道立即终止并抛出异常；
    /// 如果为 <c>false</c>（默认值），错误被记录并通过进度报告传递，管道继续处理剩余文件。
    /// </value>
    /// <remarks>
    /// <para>
    /// 当此属性为 <c>false</c> 时，单个文件的处理失败不会影响其他文件。
    /// 失败信息通过 <see cref="DiffProgress.ErrorMessage"/> 属性传递给进度报告器。
    /// 处理完成后，调用方可以检查每个文件的处理结果。
    /// </para>
    /// <para>
    /// 当此属性为 <c>true</c> 时，任何文件的失败都会取消所有正在进行的任务，
    /// 并向调用方抛出异常。适用于对数据一致性要求极高的更新场景。
    /// </para>
    /// </remarks>
    public bool StopOnFirstError { get; set; } = false;
}
