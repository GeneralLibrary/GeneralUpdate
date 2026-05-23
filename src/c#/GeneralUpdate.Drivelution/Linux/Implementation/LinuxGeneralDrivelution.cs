using System.Runtime.Versioning;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Execution;
using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Linux.Helpers;

namespace GeneralUpdate.Drivelution.Linux.Implementation;

/// <summary>
/// Linux driver updater implementation.
/// Inherits the unified pipeline from <see cref="BaseDriverUpdater"/> and adds Linux-specific
/// sudo permission check, kernel module / .deb / .rpm installation, and module parsing.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxGeneralDrivelution : BaseDriverUpdater
{
    private readonly ICommandRunner _commandRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxGeneralDrivelution"/> class.
    /// </summary>
    /// <param name="validator">Driver validator.</param>
    /// <param name="backup">Driver backup manager.</param>
    /// <param name="commandRunner">Command runner for Linux operations.</param>
    /// <param name="options">Configuration options (optional).</param>
    public LinuxGeneralDrivelution(
        IDriverValidator validator,
        IDriverBackup backup,
        ICommandRunner commandRunner,
        DrivelutionOptions? options = null)
        : base(validator, backup, options)
    {
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
    }

    // ─── Pipeline overrides ────────────────────────────────────────────

    /// <inheritdoc/>
    protected override IEnumerable<IPipelineStep> GetPipelineSteps(UpdateStrategy strategy)
    {
        yield return CreateSudoCheckStep();

        foreach (var step in base.GetPipelineSteps(strategy))
            yield return step;
    }

    /// <inheritdoc/>
    protected override async Task InstallCoreAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing Linux driver: {driverInfo.FilePath}");

        var extension = Path.GetExtension(driverInfo.FilePath).ToLowerInvariant();

        switch (extension)
        {
            case ".ko":
                await InstallKernelModuleAsync(driverInfo.FilePath, cancellationToken);
                break;
            case ".deb":
                await InstallDebPackageAsync(driverInfo.FilePath, cancellationToken);
                break;
            case ".rpm":
                await InstallRpmPackageAsync(driverInfo.FilePath, cancellationToken);
                break;
            default:
                GeneralTracer.Warn($"Unknown driver format: {extension}. Attempting generic installation.");
                await InstallKernelModuleAsync(driverInfo.FilePath, cancellationToken);
                break;
        }

        GeneralTracer.Info("Linux driver installation completed");
    }

    // ─── Rollback override ─────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task<bool> RollbackAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Rolling back Linux driver from backup: {backupPath}");

            if (!Directory.Exists(backupPath))
            {
                GeneralTracer.Error($"Backup directory not found: {backupPath}");
                return false;
            }

            var koFiles = Directory.GetFiles(backupPath, "*.ko", SearchOption.AllDirectories);

            if (koFiles.Length == 0)
            {
                GeneralTracer.Warn($"No kernel module backups found in: {backupPath}");
                return false;
            }

            foreach (var koFile in koFiles)
            {
                try
                {
                    var moduleName = Path.GetFileNameWithoutExtension(koFile);
                    GeneralTracer.Info($"Restoring kernel module: {moduleName}");

                    // Unload current module first
                    try
                    {
                        await _commandRunner.RunOrThrowAsync("modprobe", new[] { "-r", moduleName }, cancellationToken);
                    }
                    catch
                    {
                        // Module might not be loaded — ignore
                    }

                    // Reload the backed-up module
                    await _commandRunner.RunOrThrowAsync("insmod", new[] { koFile }, cancellationToken);
                    GeneralTracer.Info($"Restored module: {moduleName}");
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to restore module: {koFile} - {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Linux driver rollback failed", ex);
            return false;
        }
    }

    // ─── Driver discovery overrides ────────────────────────────────────

    /// <inheritdoc/>
    protected override string GetDefaultSearchPattern() => "*.ko";

    /// <inheritdoc/>
    public override async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        var drivers = new List<DriverInfo>();

        try
        {
            GeneralTracer.Info($"Reading Linux drivers from directory: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                GeneralTracer.Warn($"Directory not found: {directoryPath}");
                return drivers;
            }

            // Search for kernel modules, .deb, and .rpm packages
            var driverFiles = new List<string>();
            driverFiles.AddRange(Directory.GetFiles(directoryPath, searchPattern ?? "*.ko",
                SearchOption.AllDirectories));

            if (searchPattern is null)
            {
                driverFiles.AddRange(Directory.GetFiles(directoryPath, "*.deb",
                    SearchOption.AllDirectories));
                driverFiles.AddRange(Directory.GetFiles(directoryPath, "*.rpm",
                    SearchOption.AllDirectories));
            }

            GeneralTracer.Info($"Found {driverFiles.Count} Linux driver file(s)");

            foreach (var filePath in driverFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var driverInfo = await ParseLinuxDriverFileAsync(filePath, cancellationToken);
                    if (driverInfo is not null)
                        drivers.Add(driverInfo);
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
                }
            }

            GeneralTracer.Info($"Loaded {drivers.Count} Linux driver(s) from directory");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"Error reading drivers from directory: {directoryPath}", ex);
        }

        return drivers;
    }

    // ─── Private: Pipeline steps ────────────────────────────────────────

    /// <summary>
    /// Creates a sudo privilege check as the first pipeline step.
    /// </summary>
    private static IPipelineStep CreateSudoCheckStep()
    {
        return new DelegateStep("CheckSudo",
            execute: async (context, ct) =>
            {
                context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Checking Linux root privileges");

                try
                {
                    await LinuxPermissionHelper.EnsureSudoAsync();
                    return PipelineResult.Ok();
                }
                catch (Exception ex)
                {
                    return PipelineResult.Fail(
                        $"Root privileges are required for driver updates. {ex.Message}");
                }
            });
    }

    // ─── Private: Format-specific installers ───────────────────────────

    private async Task InstallKernelModuleAsync(
        string modulePath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing kernel module: {modulePath}");

        try
        {
            await _commandRunner.RunOrThrowAsync("insmod", new[] { modulePath }, cancellationToken);
            GeneralTracer.Info("Module loaded via insmod");
        }
        catch
        {
            // Fallback: modprobe with the module name
            GeneralTracer.Info("insmod failed, trying modprobe...");
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            await _commandRunner.RunOrThrowAsync("modprobe", new[] { moduleName }, cancellationToken);
            GeneralTracer.Info("Module loaded via modprobe");
        }
    }

    private async Task InstallDebPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing Debian package: {packagePath}");
        await _commandRunner.RunOrThrowAsync("dpkg", new[] { "-i", packagePath }, cancellationToken);
        GeneralTracer.Info("Debian package installed");
    }

    private async Task InstallRpmPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing RPM package: {packagePath}");

        try
        {
            await _commandRunner.RunOrThrowAsync("rpm", new[] { "-ivh", packagePath }, cancellationToken);
        }
        catch
        {
            // Fallback to dnf/yum
            await _commandRunner.RunOrThrowAsync("dnf", new[] { "install", "-y", packagePath }, cancellationToken);
        }

        GeneralTracer.Info("RPM package installed");
    }

    // ─── Private: File parsing ─────────────────────────────────────────

    private async Task<DriverInfo?> ParseLinuxDriverFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverInfo = new DriverInfo
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                TargetOS = "Linux",
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (extension)
            {
                case ".ko":
                    await ParseKernelModuleAsync(filePath, driverInfo, cancellationToken);
                    break;
                case ".deb":
                    await ParseDebPackageAsync(filePath, driverInfo, cancellationToken);
                    break;
                case ".rpm":
                    await ParseRpmPackageAsync(filePath, driverInfo, cancellationToken);
                    break;
            }

            // Compute file hash for integrity validation
            driverInfo.Hash = await HashValidator.ComputeHashAsync(
                filePath, "SHA256", cancellationToken);
            driverInfo.HashAlgorithm = "SHA256";

            return driverInfo;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
            return null;
        }
    }

    private async Task ParseKernelModuleAsync(
        string koPath,
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync("modinfo", new[] { koPath }, cancellationToken);
            var output = result.Success ? result.StandardOutput : string.Empty;
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                    driverInfo.Version = trimmed[8..].Trim();
                else if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    driverInfo.Description = trimmed[12..].Trim();
                else if (trimmed.StartsWith("alias:", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(driverInfo.HardwareId))
                        driverInfo.HardwareId = trimmed[6..].Trim();
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
                driverInfo.Version = "1.0.0";
        }
        catch (Exception ex)
        {
            GeneralTracer.Debug($"Could not get module info for {koPath}: {ex.Message}");
            driverInfo.Version = "1.0.0";
        }
    }

    private async Task ParseDebPackageAsync(
        string debPath,
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            // No shell escaping needed — ArgumentList bypasses the shell entirely
            var result = await _commandRunner.RunAsync("dpkg-deb", new[] { "-I", debPath }, cancellationToken);
            var output = result.Success ? result.StandardOutput : string.Empty;

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                    driverInfo.Version = trimmed[8..].Trim();
                else if (trimmed.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                    driverInfo.Description = trimmed[12..].Trim();
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
                driverInfo.Version = "1.0.0";
        }
        catch (Exception ex)
        {
            GeneralTracer.Debug($"Could not get package info for {debPath}: {ex.Message}");
            driverInfo.Version = "1.0.0";
        }
    }

    private async Task ParseRpmPackageAsync(
        string rpmPath,
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            // No shell escaping needed — ArgumentList bypasses the shell entirely
            var result = await _commandRunner.RunAsync("rpm", new[] { "-qip", rpmPath }, cancellationToken);
            var output = result.Success ? result.StandardOutput : string.Empty;

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length > 1)
                        driverInfo.Version = parts[1].Trim();
                }
                else if (trimmed.StartsWith("Summary", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length > 1)
                        driverInfo.Description = parts[1].Trim();
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
                driverInfo.Version = "1.0.0";
        }
        catch (Exception ex)
        {
            GeneralTracer.Debug($"Could not get package info for {rpmPath}: {ex.Message}");
            driverInfo.Version = "1.0.0";
        }
    }

    // ─── Nested type ──────────────────────────────────────────────────

    /// <summary>
    /// A lightweight pipeline step backed by delegates.
    /// </summary>
    private sealed class DelegateStep : IPipelineStep
    {
        private readonly Func<PipelineContext, CancellationToken, Task<PipelineResult>> _execute;

        public string StepName { get; }

        public DelegateStep(
            string stepName,
            Func<PipelineContext, CancellationToken, Task<PipelineResult>> execute)
        {
            StepName = stepName;
            _execute = execute;
        }

        public bool ShouldExecute(PipelineContext context) => true;

        public Task<PipelineResult> ExecuteAsync(
            PipelineContext context,
            CancellationToken cancellationToken)
            => _execute(context, cancellationToken);
    }
}
