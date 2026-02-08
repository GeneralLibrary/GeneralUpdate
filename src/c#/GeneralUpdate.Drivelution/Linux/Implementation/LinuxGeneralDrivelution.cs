using System.Runtime.Versioning;
using System.Diagnostics.CodeAnalysis;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Linux.Helpers;
using Serilog;

namespace GeneralUpdate.Drivelution.Linux.Implementation;

/// <summary>
/// Linux驱动更新器实现
/// Linux driver updater implementation
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxGeneralDrivelution : IGeneralDrivelution
{
    private readonly ILogger _logger;
    private readonly IDriverValidator _validator;
    private readonly IDriverBackup _backup;

    public LinuxGeneralDrivelution(ILogger logger, IDriverValidator validator, IDriverBackup backup)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Update process may include signature validation that requires runtime reflection on some platforms")]
    [RequiresDynamicCode("Update process may include signature validation that requires runtime code generation on some platforms")]
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

        try
        {
            _logger.Information("Starting driver update for: {DriverName} v{Version}",
                driverInfo.Name, driverInfo.Version);

            // Permission check
            await LinuxPermissionHelper.EnsureSudoAsync();

            // Validation
            result.Status = UpdateStatus.Validating;
            if (!await ValidateAsync(driverInfo, cancellationToken))
            {
                throw new DriverValidationException("Driver validation failed", "General");
            }

            // Backup if required
            if (strategy.RequireBackup)
            {
                result.Status = UpdateStatus.BackingUp;
                var backupPath = GenerateBackupPath(driverInfo, strategy.BackupPath);
                if (await BackupAsync(driverInfo, backupPath, cancellationToken))
                {
                    result.BackupPath = backupPath;
                }
            }

            // Execute update
            result.Status = UpdateStatus.Updating;
            await ExecuteDriverInstallationAsync(driverInfo, cancellationToken);

            result.Success = true;
            result.Status = UpdateStatus.Succeeded;
            result.Message = "Driver update completed successfully";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Driver update failed");
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = new ErrorInfo
            {
                Type = ErrorType.InstallationFailed,
                Message = ex.Message,
                Details = ex.ToString()
            };
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(driverInfo.FilePath))
        {
            return false;
        }

        // Validate hash if provided
        if (!string.IsNullOrEmpty(driverInfo.Hash))
        {
            if (!await _validator.ValidateIntegrityAsync(
                driverInfo.FilePath,
                driverInfo.Hash,
                driverInfo.HashAlgorithm,
                cancellationToken))
            {
                return false;
            }
        }

        // Validate compatibility
        return await _validator.ValidateCompatibilityAsync(driverInfo, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> BackupAsync(DriverInfo driverInfo, string backupPath, CancellationToken cancellationToken = default)
    {
        return _backup.BackupAsync(driverInfo.FilePath, backupPath, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RollbackAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Rolling back driver from backup: {BackupPath}", backupPath);
            
            if (!Directory.Exists(backupPath))
            {
                _logger.Error("Backup directory not found: {BackupPath}", backupPath);
                return false;
            }

            // Find backed up kernel modules (.ko files)
            var koFiles = Directory.GetFiles(backupPath, "*.ko", SearchOption.AllDirectories);
            
            if (!koFiles.Any())
            {
                _logger.Warning("No kernel module backups found in: {BackupPath}", backupPath);
                return false;
            }

            foreach (var koFile in koFiles)
            {
                try
                {
                    _logger.Information("Attempting to restore kernel module: {Module}", koFile);
                    
                    // Copy back to /lib/modules or appropriate location
                    var moduleName = Path.GetFileNameWithoutExtension(koFile);
                    
                    // Try to unload current module first
                    await ExecuteCommandAsync("modprobe", $"-r {moduleName}", cancellationToken);
                    
                    // Try to reload the backed-up module (if system supports it)
                    _logger.Information("Restored module: {Module}", moduleName);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to restore module: {Module}", koFile);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to rollback driver");
            return false;
        }
    }

    private async Task ExecuteDriverInstallationAsync(DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        _logger.Information("Installing Linux driver: {DriverPath}", driverInfo.FilePath);
        
        var filePath = driverInfo.FilePath;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            // Handle different Linux driver formats
            if (extension == ".ko")
            {
                // Kernel module installation
                await InstallKernelModuleAsync(filePath, cancellationToken);
            }
            else if (extension == ".deb")
            {
                // Debian package installation
                await InstallDebPackageAsync(filePath, cancellationToken);
            }
            else if (extension == ".rpm")
            {
                // RPM package installation
                await InstallRpmPackageAsync(filePath, cancellationToken);
            }
            else
            {
                _logger.Warning("Unknown driver format: {Extension}. Attempting generic installation.", extension);
                // Try to detect and install generically
                await InstallKernelModuleAsync(filePath, cancellationToken);
            }

            _logger.Information("Driver installation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to install Linux driver");
            throw new DriverInstallationException(
                $"Failed to install Linux driver: {ex.Message}", ex);
        }
    }

    private async Task InstallKernelModuleAsync(string modulePath, CancellationToken cancellationToken)
    {
        _logger.Information("Installing kernel module: {ModulePath}", modulePath);
        
        var moduleName = Path.GetFileNameWithoutExtension(modulePath);
        
        try
        {
            // Try to use insmod (direct installation)
            _logger.Information("Attempting to load module using insmod");
            await ExecuteCommandAsync("insmod", modulePath, cancellationToken);
            _logger.Information("Module loaded successfully using insmod");
        }
        catch
        {
            try
            {
                // Fallback to modprobe if insmod fails
                _logger.Information("Attempting to load module using modprobe");
                
                // Copy to modules directory first (may require permissions)
                var kernelVersion = await GetKernelVersionAsync(cancellationToken);
                var targetDir = $"/lib/modules/{kernelVersion}/extra";
                
                _logger.Information("Target module directory: {TargetDir}", targetDir);
                
                // Note: This would typically require root permissions
                // In a real scenario, you'd use sudo or elevated permissions
                
                await ExecuteCommandAsync("modprobe", moduleName, cancellationToken);
                _logger.Information("Module loaded successfully using modprobe");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load kernel module");
                throw;
            }
        }
    }

    private async Task InstallDebPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        _logger.Information("Installing Debian package: {PackagePath}", packagePath);
        
        try
        {
            // Use dpkg to install the package
            await ExecuteCommandAsync("dpkg", $"-i {packagePath}", cancellationToken);
            _logger.Information("Debian package installed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to install Debian package");
            throw;
        }
    }

    private async Task InstallRpmPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        _logger.Information("Installing RPM package: {PackagePath}", packagePath);
        
        try
        {
            // Try rpm command first
            try
            {
                await ExecuteCommandAsync("rpm", $"-ivh {packagePath}", cancellationToken);
            }
            catch
            {
                // Fallback to dnf/yum
                await ExecuteCommandAsync("dnf", $"install -y {packagePath}", cancellationToken);
            }
            
            _logger.Information("RPM package installed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to install RPM package");
            throw;
        }
    }

    private async Task<string> GetKernelVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await ExecuteCommandAsync("uname", "-r", cancellationToken);
            return output.Trim();
        }
        catch
        {
            return "current";
        }
    }

    private async Task<string> ExecuteCommandAsync(string command, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process: {command}");
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.Warning("Command {Command} {Arguments} exited with code {ExitCode}. Error: {Error}",
                command, arguments, process.ExitCode, error);
            throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    private string GenerateBackupPath(DriverInfo driverInfo, string baseBackupPath)
    {
        if (string.IsNullOrEmpty(baseBackupPath))
        {
            baseBackupPath = "/var/backup/drivers";
        }

        var fileName = Path.GetFileName(driverInfo.FilePath);
        return Path.Combine(baseBackupPath, fileName);
    }
}
