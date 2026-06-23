namespace GeneralUpdate.Drivelution.Abstractions;

/// <summary>
/// 网络下载器接口（扩展点）
/// Network downloader interface (extension point)
/// </summary>
/// <remarks>
/// 该接口预留用于未来扩展网络下载功能
/// This interface is reserved for future network download functionality
/// </remarks>
public interface INetworkDownloader
{
    /// <summary>
    /// 异步下载驱动文件
    /// Downloads driver file asynchronously
    /// </summary>
    /// <param name="url">下载地址 / Download URL</param>
    /// <param name="targetPath">目标路径 / Target path</param>
    /// <param name="progress">下载进度回调 / Download progress callback</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>下载结果 / Download result</returns>
    Task<bool> DownloadAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消下载
    /// Cancels download
    /// </summary>
    void CancelDownload();
}
