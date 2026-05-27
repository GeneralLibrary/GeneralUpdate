using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Models;

namespace GeneralUpdate.Core.Download.Abstractions;

/// <summary>Source of download assets (e.g. HTTP API, OSS bucket, SignalR Hub).</summary>
public interface IDownloadSource
{
    Task<DownloadSourceResult> ListAsync(CancellationToken token = default);
}

/// <summary>Post-download processing pipeline (verify, decompress, decrypt).</summary>
public interface IDownloadPipeline
{
    Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default);
}
