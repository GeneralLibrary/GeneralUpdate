using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Executors;

/// <summary>
/// 基于 HTTP 协议的文件下载执行器，支持断点续传（Range 请求头）和分块流式下载。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IDownloadExecutor"/> 接口，提供从 HTTP 端点下载文件的核心功能。
/// </para>
/// <para>
/// 主要特性：
/// <list type="bullet">
///   <item><term>断点续传</term><description>通过 HTTP Range 请求头从上次中断位置继续下载，
///        支持通过 <c>enableResume</c> 参数启用或禁用。当服务器不支持部分内容响应时，
///        自动回退到从头下载。</description></item>
///   <item><term>分块流式下载</term><description>使用 8KB 缓冲区逐块读取和写入数据流，
///        避免将整个文件加载到内存中。</description></item>
///   <item><term>进度报告</term><description>通过 <c>IProgress&lt;DownloadProgress&gt;</c>
///        每 250 毫秒报告一次下载进度，包含已下载字节数、总字节数和百分比。</description></item>
///   <item><term>超时控制</term><description>支持通过 <c>timeout</c> 参数设置每次请求的超时时间。</description></item>
///   <item><term>认证支持</term><description>从 <c>DownloadAsset</c> 中读取认证方案和令牌，
///        为需要授权的下载源提供 HTTP Bearer 或自定义认证。</description></item>
///   <item><term>取消支持</term><description>通过 <c>CancellationToken</c> 支持取消正在进行的下载操作。</description></item>
/// </list>
/// </para>
/// <para>
/// 此执行器被 <c>DefaultDownloadOrchestrator</c> 和 <c>OSSUpdateStrategy</c> 等高层组件使用，
/// 作为实际的 HTTP 下载引擎。
/// </para>
/// </remarks>
public class HttpDownloadExecutor : IDownloadExecutor
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly bool _enableResume;

    /// <summary>
    /// 使用指定的 HTTP 客户端、超时设置和断点续传选项初始化下载执行器。
    /// </summary>
    /// <param name="client">用于发送 HTTP 请求的 <see cref="HttpClient"/> 实例。不能为 null。</param>
    /// <param name="timeout">每次 HTTP 请求的超时时间。默认为 30 秒。</param>
    /// <param name="enableResume">是否启用断点续传功能。默认为 true。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="client"/> 为 null 时抛出。</exception>
    public HttpDownloadExecutor(HttpClient client, TimeSpan? timeout = null, bool enableResume = true)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _enableResume = enableResume;
    }

    /// <summary>
    /// 异步执行单个下载资产的文件下载操作。
    /// </summary>
    /// <param name="asset">要下载的资产信息，包含 URL、文件名、大小和哈希值。</param>
    /// <param name="destPath">下载文件的目标本地路径。</param>
    /// <param name="progress">可选的进度报告器，用于报告下载进度。</param>
    /// <param name="token">可选的取消令牌，用于取消下载操作。</param>
    /// <returns>包含下载结果（成功/失败、已下载字节数、耗时等）的 <see cref="DownloadResult"/>。</returns>
    /// <remarks>
    /// <para>
    /// 下载流程如下：
    /// </para>
    /// <list type="number">
    ///   <item>检查目标文件是否已存在部分下载（断点续传支持）。</item>
    ///   <item>创建 HTTP GET 请求，如果启用续传且存在部分文件，则添加 Range 请求头。</item>
    ///   <item>如果资产提供了认证方案和令牌，添加 Authorization 请求头。</item>
    ///   <item>发送请求并读取响应流。</item>
    ///   <item>如果服务器返回非 206 PartialContent 状态码，则从头开始下载。</item>
    ///   <item>使用 <see cref="StreamDownloadAsync"/> 分块读取并写入文件。</item>
    ///   <item>下载完成后报告 100% 进度。</item>
    /// </list>
    /// <para>
    /// 如果下载过程中发生异常（除 <c>OperationCanceledException</c> 外），
    /// 会返回包含错误信息的 <c>DownloadResult</c>，而不是抛出异常。
    /// </para>
    /// </remarks>
    public async Task<DownloadResult> ExecuteAsync(
        DownloadAsset asset, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        int retries = 0;
        long existingBytes = 0;

        // Check for existing partial file (resume support; skip when disabled)
        if (_enableResume && File.Exists(destPath))
        {
            existingBytes = new FileInfo(destPath).Length;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url);
            if (_enableResume && existingBytes > 0)
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);

            // Apply per-asset auth if provided by server (e.g. GeneralSpacestation signed URLs or Bearer tokens)
            if (!string.IsNullOrEmpty(asset.AuthScheme) && !string.IsNullOrEmpty(asset.AuthToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(asset.AuthScheme, asset.AuthToken);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(_timeout);

            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            if (_enableResume && existingBytes > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                existingBytes = 0;
                File.Delete(destPath);
            }

            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;

            var mode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
            using var fs = new FileStream(destPath, mode, FileAccess.Write, FileShare.None);
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var (downloaded, elapsed) = await StreamDownloadAsync(stream, fs, totalBytes, existingBytes,
                destPath, progress, sw, token).ConfigureAwait(false);

            progress?.Report(new DownloadProgress(
                Path.GetFileName(destPath), downloaded,
                totalBytes > 0 ? totalBytes + existingBytes : null,
                100, DownloadStatus.Completed));

            return new DownloadResult(asset, destPath, downloaded, elapsed, retries, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new DownloadResult(asset, destPath, existingBytes, sw.Elapsed, retries, false, ex.Message);
        }
    }

    /// <summary>
    /// 共享的下载循环：从源流读取数据，写入目标流，并报告下载进度。
    /// 此方法被 HTTP 和 OSS 执行器共用，避免重复的缓冲/读取/写入/进度报告逻辑。
    /// </summary>
    /// <param name="source">源数据流（通常来自 HTTP 响应流）。</param>
    /// <param name="dest">目标文件流。</param>
    /// <param name="totalBytes">服务器报告的内容总大小（可为 -1 表示未知）。</param>
    /// <param name="existingBytes">已存在的部分下载字节数（断点续传支持）。</param>
    /// <param name="destPath">目标文件路径（仅用于进度报告中的文件名显示）。</param>
    /// <param name="progress">可选的进度报告器。</param>
    /// <param name="sw">用于测量下载耗时的计时器。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>包含实际下载字节数和耗时的元组。</returns>
    /// <remarks>
    /// <para>
    /// 下载循环使用 8192 字节（8KB）的缓冲区进行分块读取，避免内存占用过高。
    /// </para>
    /// <para>
    /// 进度报告每 250 毫秒触发一次，或在下载完成时立即触发。
    /// 进度信息包含文件名、已下载字节数、总字节数（如果已知）以及完成百分比。
    /// </para>
    /// </remarks>
    internal static async Task<(long Downloaded, TimeSpan Elapsed)> StreamDownloadAsync(
        Stream source, Stream dest, long totalBytes, long existingBytes,
        string destPath, IProgress<DownloadProgress>? progress, Stopwatch sw, CancellationToken token)
    {
        var buffer = new byte[8192];
        long downloaded = existingBytes;
        long lastReport = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
        {
            await dest.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
            downloaded += read;

            var now = sw.ElapsedMilliseconds;
            if (now - lastReport >= 250 || downloaded == totalBytes + existingBytes)
            {
                lastReport = now;
                var pct = totalBytes > 0 ? (double)downloaded / (totalBytes + existingBytes) * 100 : -1;
                progress?.Report(new DownloadProgress(
                    Path.GetFileName(destPath), downloaded,
                    totalBytes > 0 ? totalBytes + existingBytes : null,
                    pct, DownloadStatus.Downloading));
            }
        }

        sw.Stop();
        return (downloaded, sw.Elapsed);
    }
}
