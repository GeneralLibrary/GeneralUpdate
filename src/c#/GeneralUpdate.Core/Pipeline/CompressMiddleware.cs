using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// 解压缩中间件，负责将下载的压缩包解压到目标目录。
/// </summary>
/// <remarks>
/// <para>
/// 此中间件从 <see cref="PipelineContext"/> 中读取以下键：
/// <list type="bullet">
///   <item><description><c>"Format"</c> — <see cref="Configuration.Format"/> 压缩包格式（如 ZIP、GZip）。</description></item>
///   <item><description><c>"ZipFilePath"</c> — 压缩包源文件路径。</description></item>
///   <item><description><c>"PatchPath"</c> — 差异补丁临时目录路径。</description></item>
///   <item><description><c>"Encoding"</c> — <see cref="Encoding"/> 解压时使用的字符编码。</description></item>
///   <item><description><c>"SourcePath"</c> — 应用程序安装目标路径。</description></item>
///   <item><description><c>"PatchEnabled"</c> — 是否启用了差异补丁模式。</description></item>
/// </list>
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item><description>读取上下文中的配置参数。</description></item>
///   <item><description>根据 <c>"PatchEnabled"</c> 的值决定解压目标路径：
///         如果启用了补丁，解压到 <c>"PatchPath"</c>；否则直接解压到 <c>"SourcePath"</c>。</description></item>
///   <item><description>调用 <see cref="CompressProvider.Decompress"/> 执行实际的解压操作。</description></item>
/// </list>
/// </para>
/// <para>
/// 此中间件应在 <see cref="HashMiddleware"/> 之后（确保压缩包完整性已验证）且
/// 在 <see cref="PatchMiddleware"/> 之前（如果需要应用差异补丁）注册。
/// </para>
/// </remarks>
public class CompressMiddleware : IMiddleware
{
    /// <summary>
    /// 异步执行解压缩操作。
    /// </summary>
    /// <param name="context">管道上下文，包含压缩包路径、格式、编码及目标路径等配置。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="System.Exception">解压缩过程中发生的任何异常。异常信息会通过 <see cref="GeneralTracer"/> 记录。</exception>
    /// <remarks>
    /// <para>
    /// 解压缩操作在后台线程上执行（通过 <see cref="Task.Run"/>），避免阻塞调用线程。
    /// 如果 <c>"PatchEnabled"</c> 为 <c>true</c>，则压缩包被解压到 <c>"PatchPath"</c> 目录，
    /// 随后由 <see cref="PatchMiddleware"/> 将补丁应用到 <c>"SourcePath"</c>。
    /// 如果为 <c>false</c>，压缩包直接解压到 <c>"SourcePath"</c>，完成更新。
    /// </para>
    /// <para>
    /// 注意：此方法不直接处理异常，而是让异常向上传播，由 <see cref="PipelineBuilder.Build"/>
    /// 负责中断管道执行。
    /// </para>
    /// </remarks>
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            var format = context.Get<Configuration.Format>("Format");
            var sourcePath = context.Get<string>("ZipFilePath");
            var patchPath = context.Get<string>("PatchPath");
            var encoding = context.Get<Encoding>("Encoding");
            var appPath = context.Get<string>("SourcePath");
            var patchEnabled = context.Get<bool?>("PatchEnabled");
            var targetPath = patchEnabled == false ? appPath : patchPath;
            GeneralTracer.Info($"CompressMiddleware.InvokeAsync: decompressing package. Format={format}, Source={sourcePath}, Target={targetPath}, PatchEnabled={patchEnabled}");
            try
            {
                CompressProvider.Decompress(format, sourcePath, targetPath, encoding);
                GeneralTracer.Info("CompressMiddleware.InvokeAsync: decompression completed successfully.");
            }
            catch (System.Exception ex)
            {
                GeneralTracer.Error("CompressMiddleware.InvokeAsync: decompression failed.", ex);
                throw;
            }
        });
    }
}
