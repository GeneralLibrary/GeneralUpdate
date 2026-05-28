using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;

namespace GeneralUpdate.Core.Download.Pipeline;

/// <summary>
/// 默认的下载后处理管道，对下载的文件执行 SHA256 哈希校验，
/// 确保文件完整性与服务器端提供的预期哈希值一致。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IDownloadPipeline"/> 接口，在下载完成后对文件进行完整性验证。
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item>检查是否配置了预期哈希值。如果没有配置，则跳过校验直接返回文件路径。</item>
///   <item>使用 SHA256 算法计算下载文件的哈希值。</item>
///   <item>将计算出的哈希值与预期的哈希值进行不区分大小写的比较。</item>
///   <item>如果哈希值匹配，返回原始文件路径。</item>
///   <item>如果哈希值不匹配，抛出 <see cref="InvalidDataException"/>。</item>
/// </list>
/// </para>
/// <para>
/// SHA256 计算在后台线程上执行（通过 <c>Task.Run</c>），避免阻塞调用线程。
/// 同时通过 <c>CancellationToken</c> 支持取消哈希计算操作。
/// </para>
/// </remarks>
public class DefaultDownloadPipeline : IDownloadPipeline
{
    private readonly string? _expectedHash;

    /// <summary>
    /// 使用预期的 SHA256 哈希值初始化下载管道。
    /// </summary>
    /// <param name="expectedHash">预期的 SHA256 哈希值（十六进制字符串，不区分大小写）。
    /// 如果为 null 或空，则跳过哈希校验。</param>
    public DefaultDownloadPipeline(string? expectedHash = null)
        => _expectedHash = expectedHash;

    /// <summary>
    /// 对已下载的文件进行处理，执行 SHA256 哈希验证。
    /// </summary>
    /// <param name="downloadedPath">已下载文件的完整路径。</param>
    /// <param name="token">用于取消哈希计算的取消令牌。</param>
    /// <returns>如果哈希验证通过，返回原始文件路径。</returns>
    /// <exception cref="InvalidDataException">当计算出的 SHA256 哈希值与预期值不匹配时抛出。</exception>
    /// <exception cref="OperationCanceledException">当操作通过取消令牌被取消时抛出。</exception>
    /// <remarks>
    /// 如果 <c>_expectedHash</c> 为 null 或空字符串，则跳过哈希校验直接返回文件路径。
    /// 哈希比较不区分大小写。
    /// </remarks>
    public async Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default)
    {
        if (!string.IsNullOrEmpty(_expectedHash))
        {
            var actual = await ComputeSha256Async(downloadedPath, token).ConfigureAwait(false);
            if (!string.Equals(actual, _expectedHash, System.StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"SHA256 mismatch for {downloadedPath}: expected {_expectedHash}, got {actual}");
        }
        return downloadedPath;
    }

    /// <summary>
    /// 计算指定文件的 SHA256 哈希值。
    /// </summary>
    /// <param name="path">要计算哈希的文件路径。</param>
    /// <param name="token">用于取消操作的取消令牌。</param>
    /// <returns>文件的小写十六进制 SHA256 哈希字符串。</returns>
    /// <remarks>
    /// 哈希计算在后台线程上执行，以避免阻塞调用线程。
    /// 返回的哈希字符串为小写字母且不含分隔符（如 "a1b2c3d4..."）。
    /// </remarks>
    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        using var sha = SHA256.Create();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await Task.Run(() => sha.ComputeHash(fs), token).ConfigureAwait(false);
        return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
