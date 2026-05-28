using System;
using System.Threading.Tasks;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// 差异补丁中间件，使用 <see cref="DiffPipeline"/> 将二进制差异补丁并行应用到应用程序文件。
/// </summary>
/// <remarks>
/// <para>
/// 此中间件从 <see cref="PipelineContext"/> 中读取以下键：
/// <list type="bullet">
///   <item><description><c>"SourcePath"</c> — 应用程序安装目标路径（旧版本文件所在目录）。</description></item>
///   <item><description><c>"PatchPath"</c> — 差异补丁文件的存放路径（包含解压后的补丁数据）。</description></item>
///   <item><description><c>"DiffPipeline"</c> — <see cref="DiffPipeline"/> 实例，由 <see cref="GeneralUpdateBootstrap"/> 构建并注入。</description></item>
/// </list>
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item><description>从上下文获取源路径、补丁路径和 <see cref="DiffPipeline"/> 实例。</description></item>
///   <item><description>调用 <see cref="DiffPipeline.DirtyAsync(string, string, IProgress{DiffProgress}, System.Threading.CancellationToken)"/>
///         将补丁并行应用到所有旧版文件上。</description></item>
///   <item><description>处理过程中的进度通过 <see cref="DiffPipeline"/> 内部的进度报告机制反馈。</description></item>
/// </list>
/// </para>
/// <para>
/// 此中间件应在 <see cref="HashMiddleware"/> 和 <see cref="CompressMiddleware"/> 之后注册，
/// 以确保压缩包已验证完整性且已正确解压到 <c>"PatchPath"</c> 目录。
/// 如果未启用差异补丁（<c>"PatchEnabled"</c> 为 <c>false</c>），此中间件不应被注册到管道中。
/// </para>
/// </remarks>
public class PatchMiddleware : IMiddleware
{
    /// <summary>
    /// 异步执行差异补丁应用逻辑。
    /// </summary>
    /// <param name="context">管道上下文，包含源路径、补丁路径和 <see cref="DiffPipeline"/> 实例。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="InvalidOperationException">
    /// 当 <see cref="PipelineContext"/> 中未包含 <c>"DiffPipeline"</c> 键时引发。
    /// 调用方需确保 <see cref="GeneralUpdateBootstrap"/> 已构建并注入了 <see cref="DiffPipeline"/> 实例。
    /// </exception>
    /// <exception cref="Exception">差异补丁应用过程中发生的其他异常。</exception>
    /// <remarks>
    /// <para>
    /// 此方法是更新管道的最终处理阶段。它将之前解压到 <c>"PatchPath"</c> 的补丁文件
    /// 并行应用到 <c>"SourcePath"</c> 中的旧版本文件上，生成更新后的文件。
    /// 补丁应用使用 <see cref="DiffPipeline.DirtyAsync(string, string, IProgress{DiffProgress}, System.Threading.CancellationToken)"/>
    /// 方法，该方法内部通过信号量控制并发度，并在所有文件处理完成后复制未知的新文件。
    /// </para>
    /// <para>
    /// 如果 <see cref="DiffPipeline"/> 未在上下文中配置，此中间件会抛出
    /// <see cref="InvalidOperationException"/> 并提供明确的错误消息，指导用户检查引导程序配置。
    /// </para>
    /// </remarks>
    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");

        var diffPipeline = context.Get<DiffPipeline>("DiffPipeline")
            ?? throw new InvalidOperationException(
                "DiffPipeline not found in PipelineContext. " +
                "Ensure GeneralUpdateBootstrap builds and injects the DiffPipeline.");

        GeneralTracer.Info($"PatchMiddleware.InvokeAsync: applying differential patch. SourcePath={sourcePath}, PatchPath={targetPath}");
        try
        {
            await diffPipeline.DirtyAsync(sourcePath, targetPath);
            GeneralTracer.Info("PatchMiddleware.InvokeAsync: differential patch applied successfully.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("PatchMiddleware.InvokeAsync: failed to apply differential patch.", ex);
            throw;
        }
    }
}
