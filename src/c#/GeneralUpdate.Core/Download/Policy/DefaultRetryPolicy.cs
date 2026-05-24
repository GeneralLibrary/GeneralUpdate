using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Download.Policy;

/// <summary>
/// Exponential backoff retry policy for downloads.
/// Retries on transient failures (timeout, network I/O, 5xx server errors).
/// Does NOT retry on permanent failures (4xx client errors, SSL/auth).
/// </summary>
public class DefaultRetryPolicy : IDownloadPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;

    public DefaultRetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, double backoffMultiplier = 2.0)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await action(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries - 1 && IsRetryable(ex))
            {
                GeneralTracer.Warn($"Download attempt {attempt + 1}/{_maxRetries} failed, retrying. {ex.Message}");
                var delay = TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt));
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is TaskCanceledException or TimeoutException) return true;
        if (ex is IOException) return true;
        if (ex is HttpRequestException hre)
        {
            var s = hre.Message ?? "";
            return s.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || s.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || s.Contains("500") || s.Contains("502")
                || s.Contains("503") || s.Contains("504");
        }
        return false;
    }
}
