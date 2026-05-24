using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;

namespace GeneralUpdate.Core.Download.Pipeline;

/// <summary>Default download pipeline: SHA256 verify the downloaded file.</summary>
public class DefaultDownloadPipeline : IDownloadPipeline
{
    private readonly string? _expectedHash;

    public DefaultDownloadPipeline(string? expectedHash = null)
        => _expectedHash = expectedHash;

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

    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        using var sha = SHA256.Create();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await Task.Run(() => sha.ComputeHash(fs), token).ConfigureAwait(false);
        return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
