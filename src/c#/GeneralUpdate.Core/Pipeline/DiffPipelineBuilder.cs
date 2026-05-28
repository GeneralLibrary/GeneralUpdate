using System;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// <see cref="DiffPipeline"/> 的流畅构建器，提供链式调用的配置方式。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipelineBuilder"/> 提供了一种声明式的方式来配置和创建
/// <see cref="DiffPipeline"/> 实例。所有配置方法都返回构建器实例本身，支持链式调用。
/// </para>
/// <para>
/// 默认配置：
/// <list type="bullet">
///   <item><description>差异比较器：<see cref="StreamingHdiffDiffer"/></description></item>
///   <item><description>清理匹配器：<see cref="DefaultCleanMatcher"/></description></item>
///   <item><description>脏匹配器：<see cref="DefaultDirtyMatcher"/></description></item>
///   <item><description>最大并行度：2</description></item>
///   <item><description>首次错误停止：<c>false</c>（继续处理其他文件）</description></item>
/// </list>
/// </para>
/// <para>
/// 使用示例：
/// <code>
/// var pipeline = new DiffPipelineBuilder()
///     .UseDiffer(new StreamingHdiffDiffer())
///     .UseCleanMatcher(new DefaultCleanMatcher())
///     .UseDirtyMatcher(new DefaultDirtyMatcher())
///     .WithParallelism(4)
///     .WithStopOnFirstError(true)
///     .WithProgress(new Progress&lt;DiffProgress&gt;(p =&gt; Console.WriteLine($"{p.Completed}/{p.Total}")))
///     .Build();
///
/// // 生成补丁
/// await pipeline.CleanAsync(oldVersionDir, newVersionDir, patchOutputDir);
///
/// // 应用补丁
/// await pipeline.DirtyAsync(appDir, patchDir);
/// </code>
/// </para>
/// </remarks>
public class DiffPipelineBuilder
{
    private IBinaryDiffer? _differ;
    private ICleanMatcher? _cleanMatcher;
    private IDirtyMatcher? _dirtyMatcher;
    private int _maxParallelism = 2;
    private bool _stopOnFirstError;
    private IProgress<DiffProgress>? _progress;

    /// <summary>
    /// 设置用于生成和应用二进制差异补丁的差异比较器。
    /// </summary>
    /// <param name="differ">实现了 <see cref="IBinaryDiffer"/> 接口的差异比较器实例。不能为 <c>null</c>。</param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="differ"/> 为 <c>null</c> 时引发。</exception>
    /// <remarks>
    /// 如果未调用此方法，默认使用 <see cref="StreamingHdiffDiffer"/>。
    /// <see cref="StreamingHdiffDiffer"/> 基于 HDiffPatch 算法实现，具有较好的压缩率和性能。
    /// 可以通过自定义实现 <see cref="IBinaryDiffer"/> 接口来使用其他差异算法。
    /// </remarks>
    public DiffPipelineBuilder UseDiffer(IBinaryDiffer differ)
    {
        _differ = differ ?? throw new ArgumentNullException(nameof(differ));
        return this;
    }

    /// <summary>
    /// 设置清理阶段（<see cref="DiffPipeline.CleanAsync"/>）使用的文件匹配器，
    /// 用于目录比较和在补丁生成过程中进行文件匹配。
    /// </summary>
    /// <param name="matcher">实现了 <see cref="ICleanMatcher"/> 接口的匹配器实例。不能为 <c>null</c>。</param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="matcher"/> 为 <c>null</c> 时引发。</exception>
    /// <remarks>
    /// <para>
    /// <see cref="ICleanMatcher"/> 负责两个关键操作：
    /// <list type="bullet">
    ///   <item><description><c>Compare</c> — 比较新旧两个目录，识别出变化的、新增的和删除的文件。</description></item>
    ///   <item><description><c>Match</c> — 在补丁生成过程中，将新版本的文件与旧版本中对应的文件进行匹配。</description></item>
    ///   <item><description><c>Except</c> — 找出旧版本中有但新版本中已删除的文件。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 如果未调用此方法，默认使用 <see cref="DefaultCleanMatcher"/>。
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder UseCleanMatcher(ICleanMatcher matcher)
    {
        _cleanMatcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        return this;
    }

    /// <summary>
    /// 设置脏阶段（<see cref="DiffPipeline.DirtyAsync"/>）使用的文件匹配器，
    /// 用于将补丁文件匹配到应用程序中对应的旧版本文件。
    /// </summary>
    /// <param name="matcher">实现了 <see cref="IDirtyMatcher"/> 接口的匹配器实例。不能为 <c>null</c>。</param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="matcher"/> 为 <c>null</c> 时引发。</exception>
    /// <remarks>
    /// <para>
    /// 在脏阶段，补丁目录中的每个补丁文件需要与应用程序目录中对应的原始文件配对。
    /// <see cref="IDirtyMatcher.Match"/> 方法接收一个旧文件对象和补丁文件列表，
    /// 返回与之匹配的补丁文件。
    /// </para>
    /// <para>
    /// 默认匹配器 <see cref="DefaultDirtyMatcher"/> 通过文件路径的相对路径进行匹配。
    /// 自定义匹配器可以实现基于文件名、哈希值或其他策略的匹配逻辑。
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder UseDirtyMatcher(IDirtyMatcher matcher)
    {
        _dirtyMatcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        return this;
    }

    /// <summary>
    /// 设置文件处理的最大并行度。
    /// </summary>
    /// <param name="maxDegreeOfParallelism">最大并行文件处理数。必须大于 0。</param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当 <paramref name="maxDegreeOfParallelism"/> 小于 1 时引发。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 此值控制 <see cref="DiffPipeline.CleanAsync"/> 和 <see cref="DiffPipeline.DirtyAsync"/>
    /// 中同时处理的文件数量。较高的值可以提高多核系统上的处理速度，但也会增加内存和 I/O 资源消耗。
    /// </para>
    /// <para>
    /// 建议：
    /// <list type="bullet">
    ///   <item><description>2（默认值）：适用于大多数场景，平衡速度和资源消耗。</description></item>
    ///   <item><description>1：完全串行处理，适用于 I/O 受限或资源敏感的环境。</description></item>
    ///   <item><description>4-8：适用于多核 CPU 和快速 SSD 的高性能环境。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithParallelism(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        _maxParallelism = maxDegreeOfParallelism;
        return this;
    }

    /// <summary>
    /// 设置是否在首次文件处理错误时立即停止整个管道。
    /// </summary>
    /// <param name="stopOnFirstError">
    /// 如果为 <c>true</c>，任何文件的处理失败都会导致整个操作立即终止并抛出异常。
    /// 如果为 <c>false</c>（默认值），失败的文件会被跳过，处理继续，错误信息通过进度报告传递。
    /// </param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <remarks>
    /// <para>
    /// 当 <paramref name="stopOnFirstError"/> 为 <c>false</c> 时（默认值）：
    /// 单个文件的失败不会影响其他文件的处理。失败的详细信息通过
    /// <see cref="DiffProgress.ErrorMessage"/> 传递，调用方可以通过进度报告机制检查每个文件的处理状态。
    /// 这在批量处理大量文件时特别有用，可以最大限度地减少失败的影響。
    /// </para>
    /// <para>
    /// 当为 <c>true</c> 时：
    /// 任何文件的失败都会立即取消所有正在进行的处理任务并抛出异常。
    /// 适用于对数据完整性要求极高的场景。
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithStopOnFirstError(bool stopOnFirstError = true)
    {
        _stopOnFirstError = stopOnFirstError;
        return this;
    }

    /// <summary>
    /// 附加一个进度报告器，用于接收实时的文件级进度更新。
    /// </summary>
    /// <param name="progress">
    /// 实现了 <see cref="IProgress{DiffProgress}"/> 接口的进度报告器实例。不能为 <c>null</c>。
    /// </param>
    /// <returns>当前 <see cref="DiffPipelineBuilder"/> 实例，支持链式调用。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="progress"/> 为 <c>null</c> 时引发。</exception>
    /// <remarks>
    /// <para>
    /// <see cref="IProgress{DiffProgress}"/> 将在每个文件处理完成时收到通知，
    /// 包含已完成数、总数、当前文件名和可选的错误消息。
    /// 可以使用 <see cref="Progress{DiffProgress}"/> 或自定义实现。
    /// </para>
    /// <para>
    /// 示例：
    /// <code>
    /// var progress = new Progress&lt;DiffProgress&gt;(p =&gt;
    /// {
    ///     Console.WriteLine($"[{p.Completed}/{p.Total}] {p.FileName}");
    ///     if (!string.IsNullOrEmpty(p.ErrorMessage))
    ///         Console.WriteLine($"    Error: {p.ErrorMessage}");
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public DiffPipelineBuilder WithProgress(IProgress<DiffProgress> progress)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        return this;
    }

    /// <summary>
    /// 使用当前配置构建 <see cref="DiffPipeline"/> 实例。
    /// </summary>
    /// <returns>配置完成的 <see cref="DiffPipeline"/> 实例。</returns>
    /// <remarks>
    /// <para>
    /// 此方法将所有配置的参数封装到 <see cref="DiffPipelineOptions"/> 中，
    /// 并使用默认值补充任何未显式设置的参数（如差异比较器默认使用 <see cref="StreamingHdiffDiffer"/>）。
    /// 然后创建一个新的 <see cref="DiffPipeline"/> 实例并返回。
    /// </para>
    /// <para>
    /// 构建后的管道可以用于生成补丁（<see cref="DiffPipeline.CleanAsync"/>）或应用补丁
    /// （<see cref="DiffPipeline.DirtyAsync"/>）。管道实例是线程安全的，可重复使用。
    /// </para>
    /// </remarks>
    public DiffPipeline Build()
    {
        var options = new DiffPipelineOptions
        {
            MaxDegreeOfParallelism = _maxParallelism,
            StopOnFirstError = _stopOnFirstError
        };

        var differ = _differ ?? new StreamingHdiffDiffer();
        return new DiffPipeline(options, differ, _cleanMatcher, _dirtyMatcher, _progress);
    }
}
