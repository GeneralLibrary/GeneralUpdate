using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>Executes a single file download.</summary>
public interface IDownloadExecutor
{
    Task<DownloadResult> ExecuteAsync(
        string url, string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default);
}

/// <summary>Retry / timeout / circuit-breaker policy for downloads.</summary>
public interface IDownloadPolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default);
}
