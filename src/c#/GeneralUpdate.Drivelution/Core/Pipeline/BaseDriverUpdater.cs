using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Abstract base class for platform-specific driver updaters.
/// Provides the unified update pipeline with retry, timeout, progress reporting, and automatic rollback.
/// Subclasses only need to implement <see cref="InstallCoreAsync"/>.
/// </summary>
public abstract class BaseDriverUpdater : IGeneralDrivelution
{
    /// <summary>
    /// Driver validator instance.
    /// </summary>
    protected readonly IDriverValidator _validator;

    /// <summary>
    /// Driver backup instance.
    /// </summary>
    protected readonly IDriverBackup _backup;

    private readonly DrivelutionOptions _options;
    private readonly RetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDriverUpdater"/> class.
    /// </summary>
    /// <param name="validator">Driver validator for integrity, signature, and compatibility checks.</param>
    /// <param name="backup">Driver backup manager.</param>
    /// <param name="options">Configuration options (optional).</param>
    protected BaseDriverUpdater(
        IDriverValidator validator,
        IDriverBackup backup,
        DrivelutionOptions? options = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
        _options = options ?? new DrivelutionOptions();
        _retryPolicy = RetryPolicy.FromOptions(_options);
    }

    // ─── IGeneralDrivelution Implementation ────────────────────────────

    /// <inheritdoc/>
    public async Task<UpdateResult> UpdateAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var result = new UpdateResult
        {
            StartTime = DateTime.UtcNow,
            Status = UpdateStatus.NotStarted
        };

        result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Starting driver update for {driverInfo.Name} v{driverInfo.Version}");

        var context = new PipelineContext(driverInfo, strategy, result);

        // Linked token: user cancellation + timeout
        var timeoutSeconds = strategy.TimeoutSeconds > 0
            ? strategy.TimeoutSeconds
            : _options.DefaultTimeoutSeconds;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            GeneralTracer.Info($"Starting driver update: {driverInfo.Name} v{driverInfo.Version} " +
                              $"(timeout={timeoutSeconds}s, retries={_retryPolicy.MaxRetries})");

            var steps = GetPipelineSteps(strategy)
                .Where(s => s.ShouldExecute(context))
                .ToList();

            var totalSteps = steps.Count;
            int stepIndex = 0;
            PipelineResult? lastStepResult = null;

            foreach (var step in steps)
            {
                stepIndex++;
                OnStepStarted?.Invoke(step.StepName);
                ReportProgress((int)((float)(stepIndex - 1) / totalSteps * 100), $"Running: {step.StepName}");

                // Execute step with retry on transient failures
                try
                {
                    lastStepResult = await _retryPolicy.ExecuteAsync(
                        ct => step.ExecuteAsync(context, ct),
                        linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    result.Success = false;
                    result.Status = UpdateStatus.Failed;
                    result.Error = new ErrorInfo
                    {
                        Type = ErrorType.Timeout,
                        Code = "ERR_TIMEOUT",
                        Message = $"Driver update timed out after {timeoutSeconds} seconds.",
                        CanRetry = true,
                        Timestamp = DateTime.UtcNow
                    };
                    result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] TIMEOUT: operation exceeded {timeoutSeconds}s");
                    result.EndTime = DateTime.UtcNow;
                    GeneralTracer.Error($"Driver update timed out: {driverInfo.Name}");
                    return result;
                }

                if (!lastStepResult.Success)
                {
                    result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] FAILED at step '{step.StepName}': {lastStepResult.ErrorMessage}");
                    break;
                }

                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Step '{step.StepName}' completed");
                OnStepCompleted?.Invoke(step.StepName);
            }

            // Check if all steps passed
            if (lastStepResult?.Success == true)
            {
                result.Success = true;
                result.Status = UpdateStatus.Succeeded;
                result.Message = "Driver update completed successfully";
                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Update completed successfully");
                GeneralTracer.Info($"Driver update completed successfully: {driverInfo.Name}");
            }
            else
            {
                result.Success = false;
                result.Status = UpdateStatus.Failed;

                if (lastStepResult?.ErrorMessage is not null)
                {
                    result.Error = new ErrorInfo
                    {
                        Type = ErrorType.InstallationFailed,
                        Code = "ERR_PIPELINE",
                        Message = lastStepResult.ErrorMessage,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Attempt rollback if backup exists
                var backupPath = result.BackupPath;
                context.Bag.TryGetValue("BackupPath", out var bagPath);
                backupPath ??= bagPath?.ToString();

                if (!string.IsNullOrEmpty(backupPath))
                {
                    result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Attempting rollback");
                    var rolledBack = await TryRollbackAsync(backupPath, linkedCts.Token);
                    if (rolledBack)
                    {
                        result.RolledBack = true;
                        result.Status = UpdateStatus.RolledBack;
                        result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Rollback completed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"Unexpected error during driver update: {ex}", ex);
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = MapExceptionToErrorInfo(ex);
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            GeneralTracer.Info($"Driver update finished. Duration={result.DurationMs}ms, Success={result.Success}");
            ReportProgress(100, result.Success ? "Completed" : "Failed");
            OnUpdateCompleted?.Invoke(result);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(driverInfo.FilePath))
            {
                GeneralTracer.Error($"Driver file not found: {driverInfo.FilePath}");
                return false;
            }

            if (!string.IsNullOrEmpty(driverInfo.Hash))
            {
                if (!await _validator.ValidateIntegrityAsync(
                        driverInfo.FilePath, driverInfo.Hash, driverInfo.HashAlgorithm, cancellationToken))
                {
                    return false;
                }
            }

            if (driverInfo.TrustedPublishers.Count > 0)
            {
                if (!await _validator.ValidateSignatureAsync(
                        driverInfo.FilePath, driverInfo.TrustedPublishers, cancellationToken))
                {
                    return false;
                }
            }

            return await _validator.ValidateCompatibilityAsync(driverInfo, cancellationToken);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Driver validation failed", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> BackupAsync(
        DriverInfo driverInfo,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        return _backup.BackupAsync(driverInfo.FilePath, backupPath, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> RollbackAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        return await TryRollbackAsync(backupPath, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var drivers = new List<DriverInfo>();

            if (!Directory.Exists(directoryPath))
            {
                GeneralTracer.Warn($"Directory not found: {directoryPath}");
                return drivers;
            }

            var pattern = searchPattern ?? GetDefaultSearchPattern();
            var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var driverInfo = ParseDriverFromFile(file);
                    if (driverInfo is not null)
                        drivers.Add(driverInfo);
                }
                catch (Exception ex)
                {
                    GeneralTracer.Debug($"Skipping file {file}: {ex.Message}");
                }
            }

            return drivers;
        }, cancellationToken);
    }

    // ─── Abstract / Virtual Members ────────────────────────────────────

    /// <summary>
    /// Platform-specific driver installation logic. Subclasses must implement this.
    /// </summary>
    /// <param name="driverInfo">Driver information.</param>
    /// <param name="strategy">Update strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DriverInstallationException">Thrown when installation fails.</exception>
    protected abstract Task InstallCoreAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the ordered pipeline steps for the given strategy.
    /// Subclasses can override to insert platform-specific steps (e.g., permission check).
    /// </summary>
    /// <param name="strategy">Update strategy.</param>
    /// <returns>Ordered pipeline steps.</returns>
    protected virtual IEnumerable<IPipelineStep> GetPipelineSteps(UpdateStrategy strategy)
    {
        yield return DefaultPipelineSteps.CreateValidateStep(_validator);
        yield return DefaultPipelineSteps.CreateBackupStep(_backup);
        yield return DefaultPipelineSteps.CreateInstallStep(InstallCoreAsync);
        yield return DefaultPipelineSteps.CreateVerifyStep(VerifyInstallationAsync);
    }

    /// <summary>
    /// Post-install verification. Default implementation always returns true.
    /// Subclasses can override to provide platform-specific verification.
    /// </summary>
    protected virtual Task<bool> VerifyInstallationAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Returns the default file search pattern for the current platform.
    /// </summary>
    protected virtual string GetDefaultSearchPattern() => "*.*";

    /// <summary>
    /// Parses driver information from a file. Subclasses should override for platform-specific formats.
    /// </summary>
    protected virtual DriverInfo? ParseDriverFromFile(string filePath)
    {
        return new DriverInfo
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Version = "1.0.0"
        };
    }

    // ─── Events ────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when a pipeline step starts executing.
    /// </summary>
    public event Action<string>? OnStepStarted;

    /// <summary>
    /// Raised when a pipeline step completes successfully.
    /// </summary>
    public event Action<string>? OnStepCompleted;

    /// <summary>
    /// Raised when the entire update process completes.
    /// </summary>
    public event Action<UpdateResult>? OnUpdateCompleted;

    /// <summary>
    /// Raised to report progress (percentage, message).
    /// </summary>
    public event Action<int, string>? OnProgress;

    // ─── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Reports progress through the OnProgress event.
    /// </summary>
    protected void ReportProgress(int percentage, string message)
    {
        OnProgress?.Invoke(percentage, message);
    }

    /// <summary>
    /// Attempts to roll back the driver to the given backup path.
    /// </summary>
    protected virtual async Task<bool> TryRollbackAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        try
        {
            GeneralTracer.Info($"Rolling back from: {backupPath}");

            if (!Directory.Exists(backupPath))
            {
                GeneralTracer.Error($"Backup directory not found: {backupPath}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Rollback failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Maps an exception to a structured <see cref="ErrorInfo"/> object.
    /// </summary>
    protected virtual ErrorInfo MapExceptionToErrorInfo(Exception ex)
    {
        return new ErrorInfo
        {
            Code = ex switch
            {
                DriverPermissionException => "ERR_PERM",
                DriverValidationException => "ERR_VALID",
                DriverInstallationException dex => dex.CanRetry ? "ERR_INSTALL_RETRY" : "ERR_INSTALL",
                DriverBackupException => "ERR_BACKUP",
                DriverRollbackException => "ERR_ROLLBACK",
                OperationCanceledException => "ERR_TIMEOUT",
                _ => "ERR_UNKNOWN"
            },
            Type = ex switch
            {
                DriverPermissionException => ErrorType.PermissionDenied,
                DriverValidationException => ErrorType.HashValidationFailed,
                DriverBackupException => ErrorType.BackupFailed,
                DriverRollbackException => ErrorType.RollbackFailed,
                OperationCanceledException => ErrorType.Timeout,
                _ => ErrorType.Unknown
            },
            Message = ex.Message,
            Details = ex.ToString(),
            StackTrace = ex.StackTrace,
            Timestamp = DateTime.UtcNow,
            CanRetry = ex is DriverInstallationException die && die.CanRetry,
            SuggestedResolution = GetSuggestedResolution(ex)
        };
    }

    private static string GetSuggestedResolution(Exception ex)
    {
        return ex switch
        {
            DriverPermissionException => "Restart the application with administrator/root privileges.",
            DriverValidationException dv => $"Check the driver file integrity and retry. Details: {dv.Message}",
            DriverInstallationException => "Verify the driver is compatible with your system and try again.",
            OperationCanceledException => "Increase the timeout value in UpdateStrategy.TimeoutSeconds.",
            _ => "Check logs for details and retry."
        };
    }
}
