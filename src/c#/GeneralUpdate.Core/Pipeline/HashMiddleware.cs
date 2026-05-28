using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// 哈希验证中间件，用于验证下载的压缩包的 SHA256 完整性。
/// </summary>
/// <remarks>
/// <para>
/// 此中间件从 <see cref="PipelineContext"/> 中读取以下键：
/// <list type="bullet">
///   <item><description><c>"ZipFilePath"</c> — 已下载的压缩包文件路径。</description></item>
///   <item><description><c>"Hash"</c> — 预期的 SHA256 哈希值（十六进制字符串）。</description></item>
/// </list>
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item><description>从上下文获取压缩包路径和期望的哈希值。</description></item>
///   <item><description>使用 <see cref="Sha256HashAlgorithm"/> 计算文件的实际 SHA256 哈希值。</description></item>
///   <item><description>将实际哈希值与期望哈希值进行不区分大小写的比较。</description></item>
///   <item><description>如果匹配，则记录成功日志并继续；如果不匹配，则抛出 <see cref="CryptographicException"/> 终止管道。</description></item>
/// </list>
/// </para>
/// <para>
/// 此中间件应在 <see cref="CompressMiddleware"/> 之前注册，确保在解压缩之前验证包的完整性。
/// </para>
/// </remarks>
public class HashMiddleware : IMiddleware
{
    /// <summary>
    /// 异步执行哈希验证逻辑。
    /// </summary>
    /// <param name="context">管道上下文，包含压缩包路径和期望的哈希值。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="CryptographicException">当实际文件的 SHA256 哈希值与期望哈希值不匹配时引发。</exception>
    /// <exception cref="Exception">文件读取或哈希计算过程中的其他异常。</exception>
    /// <remarks>
    /// <para>
    /// 此方法是管道的第一个安全检查阶段。它确保下载的压缩包在传输过程中未被篡改或损坏。
    /// </para>
    /// <para>
    /// 哈希计算在后台线程上执行（通过 <see cref="Task.Run"/>），避免阻塞调用线程。
    /// 验证失败时，<see cref="CryptographicException"/> 会向上传播，中断整个管道执行。
    /// </para>
    /// </remarks>
    public async Task InvokeAsync(PipelineContext context)
    {
        var path = context.Get<string>("ZipFilePath");
        var hash = context.Get<string>("Hash");
        GeneralTracer.Info($"HashMiddleware.InvokeAsync: verifying hash for file={path}, expectedHash={hash}");
        try
        {
            var isVerify = await VerifyFileHash(path, hash);
            if (!isVerify)
            {
                GeneralTracer.Error($"HashMiddleware.InvokeAsync: hash verification failed for file={path}.");
                throw new CryptographicException("Hash verification failed !");
            }
            GeneralTracer.Info("HashMiddleware.InvokeAsync: hash verification passed.");
        }
        catch (CryptographicException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("HashMiddleware.InvokeAsync: unexpected exception during hash verification.", ex);
            throw;
        }
    }

    /// <summary>
    /// 使用 SHA256 算法计算文件的哈希值并与期望值进行比较。
    /// </summary>
    /// <param name="path">要验证的文件完整路径。</param>
    /// <param name="hash">期望的 SHA256 哈希值（十六进制字符串）。</param>
    /// <returns>
    /// 如果文件的实际 SHA256 哈希值与 <paramref name="hash"/> 匹配（不区分大小写），则为 <c>true</c>；否则为 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 哈希计算通过 <see cref="Task.Run"/> 在后台线程池线程上执行，以避免阻塞。
    /// 内部使用 <see cref="Sha256HashAlgorithm"/> 计算文件哈希，该算法实现了标准的 SHA256 哈希计算。
    /// 比较操作用 <see cref="StringComparison.OrdinalIgnoreCase"/> 进行不区分大小写的十六进制字符串比较。
    /// </remarks>
    private Task<bool> VerifyFileHash(string path, string hash)
    {
        return Task.Run(() =>
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashSha256 = hashAlgorithm.ComputeHash(path);
            return string.Equals(hash, hashSha256, StringComparison.OrdinalIgnoreCase);
        });
    }
}
