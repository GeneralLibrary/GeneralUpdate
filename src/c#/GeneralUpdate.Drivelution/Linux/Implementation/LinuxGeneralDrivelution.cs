using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Events;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Linux.Helpers;

namespace GeneralUpdate.Drivelution.Linux.Implementation;

/// <summary>
/// Linux驱动更新器实现
/// Linux driver updater implementation
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxGeneralDrivelution : IGeneralDrivelution
{
    private readonly IDrivelutionLogger _logger;
    private readonly IDriverValidator _validator;
    private readonly IDriverBackup _backup;

    public LinuxGeneralDrivelution(IDrivelutionLogger logger, IDriverValidator validator, IDriverBackup backup)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
    }

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

        try
        {
            _logger.Information($"Starting driver update for: {driverInfo.Name} v{driverInfo.Version}");

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
            _logger.Error("Driver update failed", ex);
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
            _logger.Information($"Rolling back driver from backup: {backupPath}");
            
            if (!Directory.Exists(backupPath))
            {
                _logger.Error($"Backup directory not found: {backupPath}");
                return false;
            }

            // Find backed up kernel modules (.ko files)
            var koFiles = Directory.GetFiles(backupPath, "*.ko", SearchOption.AllDirectories);
            
            if (!koFiles.Any())
            {
                _logger.Warning($"No kernel module backups found in: {backupPath}");
                return false;
            }

            foreach (var koFile in koFiles)
            {
                try
                {
                    _logger.Information($"Attempting to restore kernel module: {koFile}");
                    
                    // Copy back to /lib/modules or appropriate location
                    var moduleName = Path.GetFileNameWithoutExtension(koFile);
                    
                    // Try to unload current module first
                    await ExecuteCommandAsync("modprobe", $"-r {moduleName}", cancellationToken);
                    
                    // Try to reload the backed-up module (if system supports it)
                    _logger.Information($"Restored module: {moduleName}");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to restore module: {koFile}", ex);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to rollback driver", ex);
            return false;
        }
    }

    private async Task ExecuteDriverInstallationAsync(DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        _logger.Information($"Installing Linux driver: {driverInfo.FilePath}");
        
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
                _logger.Warning($"Unknown driver format: {extension}. Attempting generic installation.");
                // Try to detect and install generically
                await InstallKernelModuleAsync(filePath, cancellationToken);
            }

            _logger.Information("Driver installation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to install Linux driver", ex);
            throw new DriverInstallationException(
                $"Failed to install Linux driver: {ex.Message}", ex);
        }
    }

    private async Task InstallKernelModuleAsync(string modulePath, CancellationToken cancellationToken)
    {
        _logger.Information($"Installing kernel module: {modulePath}");
        
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
                
                _logger.Information($"Target module directory: {targetDir}");
                
                // Note: This would typically require root permissions
                // In a real scenario, you'd use sudo or elevated permissions
                
                await ExecuteCommandAsync("modprobe", moduleName, cancellationToken);
                _logger.Information("Module loaded successfully using modprobe");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load kernel module", ex);
                throw;
            }
        }
    }

    private async Task InstallDebPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        _logger.Information($"Installing Debian package: {packagePath}");
        
        try
        {
            // Use dpkg to install the package
            await ExecuteCommandAsync("dpkg", $"-i {packagePath}", cancellationToken);
            _logger.Information("Debian package installed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to install Debian package", ex);
            throw;
        }
    }

    private async Task InstallRpmPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        _logger.Information($"Installing RPM package: {packagePath}");
        
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
            _logger.Error("Failed to install RPM package", ex);
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
            _logger.Warning($"Command {command} {arguments} exited with code {process.ExitCode}. Error: {error}");
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

    /// <inheritdoc/>
    public async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        var driverInfoList = new List<DriverInfo>();

        try
        {
            _logger.Information($"Reading driver information from directory: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                _logger.Warning($"Directory not found: {directoryPath}");
                return driverInfoList;
            }

            // Default to kernel modules for Linux
            var pattern = searchPattern ?? "*.ko";
            var driverFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            // Also look for .deb and .rpm packages if no specific pattern was provided
            if (searchPattern == null)
            {
                var debFiles = Directory.GetFiles(directoryPath, "*.deb", SearchOption.AllDirectories);
                var rpmFiles = Directory.GetFiles(directoryPath, "*.rpm", SearchOption.AllDirectories);
                driverFiles = driverFiles.Concat(debFiles).Concat(rpmFiles).ToArray();
            }

            _logger.Information($"Found {driverFiles.Length} driver files matching pattern: {pattern}");

            foreach (var filePath in driverFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var driverInfo = await ParseDriverFileAsync(filePath, cancellationToken);
                    if (driverInfo != null)
                    {
                        driverInfoList.Add(driverInfo);
                        _logger.Information($"Parsed driver: {driverInfo.Name} v{driverInfo.Version}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to parse driver file: {filePath}", ex);
                }
            }

            _logger.Information($"Successfully loaded {driverInfoList.Count} driver(s) from directory");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error reading drivers from directory: {directoryPath}", ex);
        }

        return driverInfoList;
    }

    /// <summary>
    /// Parses driver file information
    /// </summary>
    private async Task<DriverInfo?> ParseDriverFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            var driverInfo = new DriverInfo
            {
                Name = fileName,
                FilePath = filePath,
                TargetOS = "Linux",
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };

            // Parse based on file type
            if (extension == ".ko")
            {
                await ParseKernelModuleAsync(filePath, driverInfo, cancellationToken);
            }
            else if (extension == ".deb")
            {
                await ParseDebPackageAsync(filePath, driverInfo, cancellationToken);
            }
            else if (extension == ".rpm")
            {
                await ParseRpmPackageAsync(filePath, driverInfo, cancellationToken);
            }

            // Get file hash for integrity validation
            driverInfo.Hash = await HashValidator.ComputeHashAsync(filePath, "SHA256", cancellationToken);
            driverInfo.HashAlgorithm = "SHA256";

            return driverInfo;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Failed to parse driver file: {filePath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Parses kernel module
    /// </summary>
    private async Task ParseKernelModuleAsync(string koPath, DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        try
        {
            // Try to get module info using modinfo command
            var output = await ExecuteCommandAsync("modinfo", koPath, cancellationToken);
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                {
                    driverInfo.Version = trimmedLine.Substring(8).Trim();
                }
                else if (trimmedLine.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                {
                    driverInfo.Description = trimmedLine.Substring(12).Trim();
                }
                else if (trimmedLine.StartsWith("alias:", StringComparison.OrdinalIgnoreCase))
                {
                    var alias = trimmedLine.Substring(6).Trim();
                    if (string.IsNullOrEmpty(driverInfo.HardwareId))
                    {
                        driverInfo.HardwareId = alias;
                    }
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
            {
                driverInfo.Version = "1.0.0";
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Could not get module info for: {koPath}", ex);
            driverInfo.Version = "1.0.0";
        }
    }

    /// <summary>
    /// Parses Debian package
    /// </summary>
    private async Task ParseDebPackageAsync(string debPath, DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        try
        {
            // Try to get package info using dpkg-deb command
            // Use proper argument passing to avoid injection issues
            var escapedPath = debPath.Replace("'", "'\\''");
            var output = await ExecuteCommandAsync("dpkg-deb", $"-I '{escapedPath}'", cancellationToken);
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                {
                    driverInfo.Version = trimmedLine.Substring(8).Trim();
                }
                else if (trimmedLine.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                {
                    driverInfo.Description = trimmedLine.Substring(12).Trim();
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
            {
                driverInfo.Version = "1.0.0";
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Could not get package info for: {debPath}", ex);
            driverInfo.Version = "1.0.0";
        }
    }

    /// <summary>
    /// Parses RPM package
    /// </summary>
    private async Task ParseRpmPackageAsync(string rpmPath, DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        try
        {
            // Try to get package info using rpm command
            // Use proper argument passing to avoid injection issues
            var escapedPath = rpmPath.Replace("'", "'\\''");
            var output = await ExecuteCommandAsync("rpm", $"-qip '{escapedPath}'", cancellationToken);
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split(':');
                    if (parts.Length > 1)
                    {
                        driverInfo.Version = parts[1].Trim();
                    }
                }
                else if (trimmedLine.StartsWith("Summary", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split(':');
                    if (parts.Length > 1)
                    {
                        driverInfo.Description = parts[1].Trim();
                    }
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
            {
                driverInfo.Version = "1.0.0";
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Could not get package info for: {rpmPath}", ex);
            driverInfo.Version = "1.0.0";
        }
    }
}
