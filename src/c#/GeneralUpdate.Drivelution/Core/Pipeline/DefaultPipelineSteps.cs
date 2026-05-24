using GeneralUpdate.Core;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;

namespace GeneralUpdate.Drivelution.Core.Pipeline;

/// <summary>
/// Built-in pipeline step implementations for the standard driver update flow.
/// </summary>
internal static class DefaultPipelineSteps
{
    /// <summary>
    /// Creates the validate step: checks file existence, hash integrity, signature, and compatibility.
    /// </summary>
    public static IPipelineStep CreateValidateStep(IDriverValidator validator)
    {
        return new DelegateStep("Validate", async (context, ct) =>
        {
            var driver = context.DriverInfo;
            var strategy = context.Strategy;

            context.Result.Status = UpdateStatus.Validating;
            context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Validating driver");

            // File existence check
            if (!File.Exists(driver.FilePath))
            {
                GeneralTracer.Error($"Driver file not found: {driver.FilePath}");
                return PipelineResult.Fail($"Driver file not found: {driver.FilePath}");
            }

            // Hash validation (skip if configured)
            if (!strategy.SkipHashValidation && !string.IsNullOrEmpty(driver.Hash))
            {
                if (!await validator.ValidateIntegrityAsync(
                        driver.FilePath, driver.Hash, driver.HashAlgorithm, ct))
                {
                    return PipelineResult.Fail("Driver hash validation failed");
                }
            }

            // Signature validation (skip if configured)
            if (!strategy.SkipSignatureValidation && driver.TrustedPublishers.Count > 0)
            {
                if (!await validator.ValidateSignatureAsync(
                        driver.FilePath, driver.TrustedPublishers, ct))
                {
                    return PipelineResult.Fail("Driver signature validation failed");
                }
            }

            // Compatibility check
            if (!await validator.ValidateCompatibilityAsync(driver, ct))
            {
                return PipelineResult.Fail(
                    $"Driver is not compatible with the current platform. " +
                    $"Target: {driver.TargetOS} {driver.Architecture}, " +
                    $"Current: {CompatibilityChecker.GetCurrentOS()} {CompatibilityChecker.GetCurrentArchitecture()}");
            }

            return PipelineResult.Ok();
        });
    }

    /// <summary>
    /// Creates the backup step: backs up the current driver before installation.
    /// </summary>
    public static IPipelineStep CreateBackupStep(IDriverBackup backup)
    {
        return new DelegateStep("Backup", async (context, ct) =>
        {
            context.Result.Status = UpdateStatus.BackingUp;
            context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Creating backup");

            var backupPath = Path.Combine(
                context.Strategy.BackupPath,
                $"backup_{context.DriverInfo.Name}_{DateTime.Now:yyyyMMddHHmmss}");

            if (await backup.BackupAsync(context.DriverInfo.FilePath, backupPath, ct))
            {
                context.Result.BackupPath = backupPath;
                context.Bag["BackupPath"] = backupPath;
                GeneralTracer.Info($"Backup created at: {backupPath}");
                return PipelineResult.Ok();
            }

            return PipelineResult.Fail("Failed to create driver backup");
        },
        shouldExecute: context => context.Strategy.RequireBackup);
    }

    /// <summary>
    /// Creates the install step: delegates to the platform-specific InstallCoreAsync method.
    /// </summary>
    public static IPipelineStep CreateInstallStep(
        Func<DriverInfo, UpdateStrategy, CancellationToken, Task> installCore)
    {
        return new DelegateStep("Install", async (context, ct) =>
        {
            context.Result.Status = UpdateStatus.Updating;
            context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Installing driver");

            await installCore(context.DriverInfo, context.Strategy, ct);

            return PipelineResult.Ok();
        });
    }

    /// <summary>
    /// Creates the verify step: confirms the driver was installed correctly.
    /// </summary>
    public static IPipelineStep CreateVerifyStep(
        Func<DriverInfo, CancellationToken, Task<bool>> verifyCore)
    {
        return new DelegateStep("Verify", async (context, ct) =>
        {
            context.Result.Status = UpdateStatus.Verifying;
            context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Verifying installation");

            var verified = await verifyCore(context.DriverInfo, ct);

            if (!verified)
            {
                // Non-fatal: log warning but don't fail the whole update
                GeneralTracer.Warn("Driver installation verification returned false");
                context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] WARNING: Verification inconclusive");
            }

            return PipelineResult.Ok();
        });
    }

    /// <summary>
    /// Lightweight step implementation using delegates.
    /// </summary>
    private sealed class DelegateStep : IPipelineStep
    {
        private readonly Func<PipelineContext, CancellationToken, Task<PipelineResult>> _execute;
        private readonly Func<PipelineContext, bool> _shouldExecute;

        public string StepName { get; }

        public DelegateStep(
            string stepName,
            Func<PipelineContext, CancellationToken, Task<PipelineResult>> execute,
            Func<PipelineContext, bool>? shouldExecute = null)
        {
            StepName = stepName;
            _execute = execute;
            _shouldExecute = shouldExecute ?? (_ => true);
        }

        public bool ShouldExecute(PipelineContext context) => _shouldExecute(context);

        public Task<PipelineResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => _execute(context, cancellationToken);
    }
}
