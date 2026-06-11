using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;

namespace GeneralUpdate.Core.Download.Policy;

/// <summary>
/// Default download retry policy based on exponential backoff.
/// Retries on recoverable transient failures (timeouts, network I/O errors, 5xx server errors).
/// Does not retry on permanent failures (4xx client errors, SSL/authentication errors).
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDownloadPolicy"/> and provides a configurable retry mechanism
/// for download operations.
/// </para>
/// <para>
/// Retry policy features:
/// <list type="bullet">
///   <item><term>Exponential backoff</term><description>Delay between retries grows exponentially.
///         Formula: <c>initialDelay * backoffMultiplier^attempt</c>.
///         For example, with an initial delay of 1 second and multiplier 2.0, retry intervals are 1s, 2s, 4s, 8s, etc.</description></item>
///   <item><term>Configurable maximum retries</term><description>Defaults to 3 attempts maximum.</description></item>
///   <item><term>Retryable exception detection</term><description>Uses <c>IsRetryable</c> to precisely identify
///         which exceptions warrant a retry, avoiding wasted retries on permanent failures like 4xx client errors.</description></item>
///   <item><term>Cancellation support</term><description>Responds to <c>CancellationToken</c> cancellation requests during retry delays.</description></item>
/// </list>
/// </para>
/// <para>
/// Retryable exception types:
/// <list type="bullet">
///   <item><c>TaskCanceledException</c> — Task was cancelled (possibly due to timeout).</item>
///   <item><c>TimeoutException</c> — Operation timed out.</item>
///   <item><c>IOException</c> — Network I/O error.</item>
///   <item><c>HttpRequestException</c> containing timeout, 500, 502, 503, or 504 status codes.</item>
/// </list>
/// </para>
/// <para>
/// Non-retryable exception types:
/// <list type="bullet">
///   <item><c>OperationCanceledException</c> — User-initiated cancellation.</item>
///   <item>4xx client errors (thrown as <c>HttpRequestException</c> that will not match the retryable conditions).</item>
/// </list>
/// </para>
/// </remarks>
public class DefaultRetryPolicy : IDownloadPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRetryPolicy"/> class
    /// with the specified retry count, initial delay, and backoff multiplier.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (including the initial attempt). Defaults to 3.</param>
    /// <param name="initialDelay">Initial delay before the first retry. Defaults to 1 second.</param>
    /// <param name="backoffMultiplier">Backoff multiplier applied to the delay after each retry. Defaults to 2.0.</param>
    public DefaultRetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, double backoffMultiplier = 2.0)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
    }

    /// <summary>
    /// Asynchronously executes the specified operation, retrying on retryable exceptions
    /// using exponential backoff.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="action">The operation to execute, accepting a <see cref="CancellationToken"/> and returning a <see cref="Task{T}"/>.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the entire operation including retries.</param>
    /// <returns>The result of the operation on success.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="token"/> (this exception is never retried).</exception>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// </para>
    /// <list type="number">
    ///   <item>Executes the provided operation.</item>
    ///   <item>If the operation succeeds, returns the result immediately.</item>
    ///   <item>If the operation throws a retryable exception and the maximum retry count has not been reached,
    ///         logs a warning, waits for the computed backoff delay, and retries.</item>
    ///   <item>If the operation throws a non-retryable exception or the maximum retry count has been reached,
    ///         the exception propagates upward (not caught).</item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Determines whether the specified exception is retryable.
    /// </summary>
    /// <param name="ex">The exception to examine.</param>
    /// <returns>True if the exception is of a retryable type (timeout, network I/O, 5xx server error); otherwise false.</returns>
    /// <remarks>
    /// <para>The following exceptions are considered retryable:</para>
    /// <list type="bullet">
    ///   <item><c>TaskCanceledException</c> — Task was cancelled (typically caused by a timeout).</item>
    ///   <item><c>TimeoutException</c> — Operation timed out.</item>
    ///   <item><c>IOException</c> — Network I/O error.</item>
    ///   <item><c>HttpRequestException</c> with a message containing "timeout", "500", "502", "503", or "504".</item>
    /// </list>
    /// <para>The following exceptions are NOT considered retryable:</para>
    /// <list type="bullet">
    ///   <item><c>OperationCanceledException</c> — User-initiated cancellation.</item>
    ///   <item>4xx client errors (when thrown as <c>HttpRequestException</c>, they will not match the retryable conditions above).</item>
    /// </list>
    /// </remarks>
    private static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is TaskCanceledException or TimeoutException) return true;
        if (ex is IOException) return true;
        if (ex is HttpRequestException hre)
        {
#if NET5_0_OR_GREATER
            var statusCode = hre.StatusCode;
            if (statusCode.HasValue)
                return statusCode.Value == System.Net.HttpStatusCode.InternalServerError
                    || statusCode.Value == System.Net.HttpStatusCode.BadGateway
                    || statusCode.Value == System.Net.HttpStatusCode.ServiceUnavailable
                    || statusCode.Value == System.Net.HttpStatusCode.GatewayTimeout;
#endif
            var s = hre.Message ?? "";
            return s.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || s.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(s, @"\b(500|502|503|504)\b");
        }
        return false;
    }
}
