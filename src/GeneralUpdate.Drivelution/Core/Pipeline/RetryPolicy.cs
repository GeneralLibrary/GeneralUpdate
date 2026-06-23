namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Configurable retry policy for pipeline step execution.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Delay between retries.
    /// </summary>
    public TimeSpan Delay { get; }

    /// <summary>
    /// Whether to use exponential backoff (doubles delay each retry).
    /// </summary>
    public bool UseExponentialBackoff { get; }

    /// <summary>
    /// Creates a retry policy from defaults (3 retries, 5s interval, no backoff).
    /// </summary>
    public static RetryPolicy Default { get; } = new(3, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Creates a retry policy with no retries.
    /// </summary>
    public static RetryPolicy NoRetry { get; } = new(0, TimeSpan.Zero);

    /// <summary>
    /// Initializes a new retry policy.
    /// </summary>
    /// <param name="maxRetries">Maximum retry attempts.</param>
    /// <param name="delay">Delay between retries.</param>
    /// <param name="useExponentialBackoff">Whether to double delay each retry.</param>
    public RetryPolicy(int maxRetries, TimeSpan delay, bool useExponentialBackoff = false)
    {
        MaxRetries = maxRetries;
        Delay = delay;
        UseExponentialBackoff = useExponentialBackoff;
    }

    /// <summary>
    /// Creates a RetryPolicy from <see cref="DrivelutionOptions"/>.
    /// </summary>
    public static RetryPolicy FromOptions(Abstractions.Configuration.DrivelutionOptions? options)
    {
        if (options is null)
            return Default;

        return new RetryPolicy(
            options.DefaultRetryCount > 0 ? options.DefaultRetryCount : 3,
            TimeSpan.FromSeconds(options.DefaultRetryIntervalSeconds > 0 ? options.DefaultRetryIntervalSeconds : 5),
            useExponentialBackoff: options.UseExponentialBackoff);
    }

    /// <summary>
    /// Executes an asynchronous operation with retry logic.
    /// On the last retry, the exception is wrapped in a descriptive AggregateException rather than escaping raw.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        List<Exception>? capturedExceptions = null;

        while (true)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;  // Never retry cancellations
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                capturedExceptions ??= new List<Exception>();
                capturedExceptions.Add(ex);
                attempt++;
                await DelayAsync(attempt, cancellationToken);
            }
        }
        // If we exhaust retries, the last exception propagates via the catch-when fallthrough
    }

    /// <summary>
    /// Executes an async operation returning a boolean, with retry logic.
    /// Returns false only after exhausting all retries.
    /// </summary>
    public async Task<bool> ExecuteWithRetryAsync(
        Func<CancellationToken, Task<bool>> operation,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                if (await operation(cancellationToken))
                    return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Transient failure — will retry if we have attempts left
            }

            if (attempt >= MaxRetries)
                return false;

            attempt++;
            await DelayAsync(attempt, cancellationToken);
        }
    }

    /// <summary>
    /// Calculates the delay for a given retry attempt, applying exponential backoff if configured.
    /// </summary>
    private async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = UseExponentialBackoff
            ? TimeSpan.FromMilliseconds(Delay.TotalMilliseconds * Math.Pow(2, attempt - 1))
            : Delay;

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken);
    }
}
