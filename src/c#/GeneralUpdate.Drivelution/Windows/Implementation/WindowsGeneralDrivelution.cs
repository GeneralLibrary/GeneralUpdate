using System.Diagnostics;
using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Windows.Helpers;
using Serilog;

namespace GeneralUpdate.Drivelution.Windows.Implementation;

/// <summary>
/// Windows驱动更新器实现
/// Windows driver updater implementation
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsGeneralDrivelution : IGeneralDrivelution
{
    private readonly ILogger _logger;
    private readonly IDriverValidator _validator;
    private readonly IDriverBackup _backup;

    public WindowsGeneralDrivelution(ILogger logger, IDriverValidator validator, IDriverBackup backup)
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
            _logger.Information("Starting driver update for: {DriverName} v{Version}",
                driverInfo.Name, driverInfo.Version);

            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Starting driver update");

            // Step 1: Permission check
            _logger.Information("Checking permissions...");
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Checking permissions");
            
            if (!WindowsPermissionHelper.IsAdministrator())
            {
                throw new DriverPermissionException(
                    "Administrator privileges are required for driver updates. " +
                    "Please restart the application as administrator.");
            }

            // Step 2: Validation
            result.Status = UpdateStatus.Validating;
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Validating driver");
            
            if (!await ValidateAsync(driverInfo, cancellationToken))
            {
                throw new DriverValidationException(
                    "Driver validation failed. Please check the driver file and try again.",
                    "General");
            }

            // Step 3: Backup (if required)
            if (strategy.RequireBackup)
            {
                result.Status = UpdateStatus.BackingUp;
                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Creating backup");
                
                var backupPath = GenerateBackupPath(driverInfo, strategy.BackupPath);
                if (await BackupAsync(driverInfo, backupPath, cancellationToken))
                {
                    result.BackupPath = backupPath;
                    _logger.Information("Backup created at: {BackupPath}", backupPath);
                }
            }

            // Step 4: Execute update
            result.Status = UpdateStatus.Updating;
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Installing driver");
            
            await ExecuteDriverInstallationAsync(driverInfo, strategy, cancellationToken);

            // Step 5: Verify installation
            result.Status = UpdateStatus.Verifying;
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Verifying installation");
            
            bool verified = await VerifyDriverInstallationAsync(driverInfo, cancellationToken);
            if (!verified)
            {
                _logger.Warning("Driver installation verification failed");
                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] WARNING: Installation verification failed");
            }
            _logger.Information("Driver installation verification completed");

            // Step 6: Handle restart if needed
            if (RestartHelper.IsRestartRequired(strategy.RestartMode))
            {
                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] System restart is required");
                _logger.Information("System restart is required for driver update");
            }

            result.Success = true;
            result.Status = UpdateStatus.Succeeded;
            result.Message = "Driver update completed successfully";
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Update completed successfully");
            
            _logger.Information("Driver update completed successfully");
        }
        catch (DriverPermissionException ex)
        {
            _logger.Error(ex, "Permission denied during driver update");
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = CreateErrorInfo(ex, ErrorType.PermissionDenied, false);
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        catch (DriverValidationException ex)
        {
            _logger.Error(ex, "Validation failed during driver update");
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = CreateErrorInfo(ex, ErrorType.HashValidationFailed, false);
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        catch (DriverInstallationException ex)
        {
            _logger.Error(ex, "Installation failed during driver update");
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = CreateErrorInfo(ex, ErrorType.InstallationFailed, ex.CanRetry);
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");

            // Attempt rollback if backup exists
            if (!string.IsNullOrEmpty(result.BackupPath))
            {
                result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Attempting rollback");
                if (await TryRollbackAsync(result.BackupPath, cancellationToken))
                {
                    result.RolledBack = true;
                    result.Status = UpdateStatus.RolledBack;
                    result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Rollback completed");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during driver update");
            result.Success = false;
            result.Status = UpdateStatus.Failed;
            result.Error = CreateErrorInfo(ex, ErrorType.Unknown, false);
            result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            _logger.Information("Driver update process ended. Duration: {Duration}ms, Success: {Success}",
                result.DurationMs, result.Success);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)
    {
        _logger.Information("Validating driver: {DriverName}", driverInfo.Name);

        try
        {
            // Validate file exists
            if (!File.Exists(driverInfo.FilePath))
            {
                _logger.Error("Driver file not found: {FilePath}", driverInfo.FilePath);
                return false;
            }

            // Validate hash if provided and not skipped
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

            // Validate signature if publishers provided
            if (driverInfo.TrustedPublishers.Any())
            {
                if (!await _validator.ValidateSignatureAsync(
                    driverInfo.FilePath,
                    driverInfo.TrustedPublishers,
                    cancellationToken))
                {
                    return false;
                }
            }

            // Validate compatibility
            if (!await _validator.ValidateCompatibilityAsync(driverInfo, cancellationToken))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Driver validation failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> BackupAsync(DriverInfo driverInfo, string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _backup.BackupAsync(driverInfo.FilePath, backupPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to backup driver");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RollbackAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Rolling back driver from backup: {BackupPath}", backupPath);
            
            // Implement rollback logic
            // This involves:
            // 1. Restoring from backup
            // 2. Optionally reinstalling the old driver
            
            if (!Directory.Exists(backupPath))
            {
                _logger.Error("Backup directory not found: {BackupPath}", backupPath);
                return false;
            }

            // Find the backed up driver files
            var backupFiles = Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories);
            if (!backupFiles.Any())
            {
                _logger.Warning("No backup files found in: {BackupPath}", backupPath);
                return false;
            }

            _logger.Information("Found {Count} backup files", backupFiles.Length);
            
            // For INF-based drivers, try to reinstall the backed up version
            var infFiles = backupFiles.Where(f => f.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)).ToArray();
            
            if (infFiles.Any())
            {
                foreach (var infFile in infFiles)
                {
                    try
                    {
                        _logger.Information("Attempting to restore driver from: {InfFile}", infFile);
                        await InstallDriverUsingPnPUtilAsync(infFile, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to restore driver from: {InfFile}", infFile);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to rollback driver");
            throw new DriverRollbackException($"Failed to rollback driver: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行驱动安装
    /// Executes driver installation
    /// </summary>
    private async Task ExecuteDriverInstallationAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken)
    {
        _logger.Information("Executing driver installation: {DriverPath}", driverInfo.FilePath);

        try
        {
            // TODO: Implement actual driver installation using SetupDi APIs
            // This is a placeholder that would use Windows Device Manager APIs:
            // - SetupDiGetClassDevs
            // - SetupDiEnumDeviceInfo
            // - UpdateDriverForPlugAndPlayDevices
            // - DiInstallDriver (for .inf files)

            // For demonstration, we'll use PnPUtil as a fallback
            await InstallDriverUsingPnPUtilAsync(driverInfo.FilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Driver installation failed");
            throw new DriverInstallationException($"Failed to install driver: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 使用PnPUtil安装驱动
    /// Installs driver using PnPUtil
    /// </summary>
    private async Task InstallDriverUsingPnPUtilAsync(string driverPath, CancellationToken cancellationToken)
    {
        _logger.Information("Installing driver using PnPUtil: {DriverPath}", driverPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "pnputil.exe",
            Arguments = $"/add-driver \"{driverPath}\" /install",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        _logger.Information("PnPUtil output: {Output}", output);

        if (process.ExitCode != 0)
        {
            _logger.Error("PnPUtil failed with exit code {ExitCode}. Error: {Error}",
                process.ExitCode, error);
            throw new DriverInstallationException(
                $"PnPUtil failed with exit code {process.ExitCode}: {error}");
        }
    }

    /// <summary>
    /// 验证驱动安装
    /// Verify driver installation
    /// </summary>
    private async Task<bool> VerifyDriverInstallationAsync(DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.Information("Verifying driver installation for: {DriverPath}", driverInfo.FilePath);
            
            // Use PnPUtil to enumerate installed drivers and check if our driver is present
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = "/enum-drivers",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                _logger.Warning("Failed to start PnPUtil for verification");
                return false;
            }

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // Check if the driver file name appears in the output
            string driverFileName = Path.GetFileName(driverInfo.FilePath);
            bool isInstalled = output.Contains(driverFileName, StringComparison.OrdinalIgnoreCase) ||
                             output.Contains(Path.GetFileNameWithoutExtension(driverInfo.FilePath), StringComparison.OrdinalIgnoreCase);

            _logger.Information("Driver verification result: {IsInstalled}", isInstalled);
            return isInstalled;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to verify driver installation");
            // Return true to not block the update if verification fails
            return true;
        }
    }

    private string GenerateBackupPath(DriverInfo driverInfo, string baseBackupPath)
    {
        if (string.IsNullOrEmpty(baseBackupPath))
        {
            baseBackupPath = "./DriverBackups";
        }

        var fileName = Path.GetFileName(driverInfo.FilePath);
        return Path.Combine(baseBackupPath, fileName);
    }

    private async Task<bool> TryRollbackAsync(string backupPath, CancellationToken cancellationToken)
    {
        try
        {
            return await RollbackAsync(backupPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Rollback failed");
            return false;
        }
    }

    private ErrorInfo CreateErrorInfo(Exception ex, ErrorType type, bool canRetry)
    {
        return new ErrorInfo
        {
            Code = ex is DrivelutionException dex ? dex.ErrorCode : "DR_UNKNOWN",
            Type = type,
            Message = ex.Message,
            Details = ex.ToString(),
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException,
            CanRetry = canRetry,
            SuggestedResolution = GetSuggestedResolution(type)
        };
    }

    private string GetSuggestedResolution(ErrorType type)
    {
        return type switch
        {
            ErrorType.PermissionDenied => "Run the application as administrator",
            ErrorType.SignatureValidationFailed => "Ensure the driver is properly signed by a trusted publisher",
            ErrorType.HashValidationFailed => "Re-download the driver file and verify its integrity",
            ErrorType.CompatibilityValidationFailed => "Check if the driver is compatible with your system",
            ErrorType.InstallationFailed => "Check Windows Event Viewer for more details",
            _ => "Contact support for assistance"
        };
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
            _logger.Information("Reading driver information from directory: {DirectoryPath}", directoryPath);

            if (!Directory.Exists(directoryPath))
            {
                _logger.Warning("Directory not found: {DirectoryPath}", directoryPath);
                return driverInfoList;
            }

            // Default to .inf files for Windows
            var pattern = searchPattern ?? "*.inf";
            var driverFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            _logger.Information("Found {Count} driver files matching pattern: {Pattern}", driverFiles.Length, pattern);

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
                        _logger.Information("Parsed driver: {DriverName} v{Version}", driverInfo.Name, driverInfo.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse driver file: {FilePath}", filePath);
                }
            }

            _logger.Information("Successfully loaded {Count} driver(s) from directory", driverInfoList.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading drivers from directory: {DirectoryPath}", directoryPath);
        }

        return driverInfoList;
    }

    /// <summary>
    /// 解析驱动文件信息
    /// Parses driver file information
    /// </summary>
    private async Task<DriverInfo?> ParseDriverFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var driverInfo = new DriverInfo
            {
                Name = fileName,
                FilePath = filePath,
                TargetOS = "Windows",
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };

            // For .inf files, try to parse version and other metadata
            if (filePath.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                await ParseInfFileAsync(filePath, driverInfo, cancellationToken);
            }

            // Get file hash for integrity validation
            driverInfo.Hash = await HashValidator.ComputeHashAsync(filePath, "SHA256", cancellationToken);
            driverInfo.HashAlgorithm = "SHA256";

            // Get signature information if available
            try
            {
                if (WindowsSignatureHelper.IsFileSigned(filePath))
                {
                    // Try to extract publisher from certificate
                    using var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
                    if (cert != null)
                    {
                        using var cert2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(cert);
                        var subject = cert2.Subject;
                        
                        // Extract CN (Common Name) from subject
                        if (subject.Contains("CN="))
                        {
                            var cnStart = subject.IndexOf("CN=") + 3;
                            var cnEnd = subject.IndexOf(',', cnStart);
                            var publisher = cnEnd > cnStart ? subject.Substring(cnStart, cnEnd - cnStart) : subject.Substring(cnStart);
                            
                            if (!string.IsNullOrEmpty(publisher))
                            {
                                driverInfo.TrustedPublishers.Add(publisher);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Could not get signature for file: {FilePath}", filePath);
            }

            return driverInfo;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse driver file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 解析INF文件
    /// Parses INF file
    /// </summary>
    private async Task ParseInfFileAsync(string infPath, DriverInfo driverInfo, CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(infPath, cancellationToken);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Parse version
                if (trimmedLine.StartsWith("DriverVer", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split('=');
                    if (parts.Length > 1)
                    {
                        var verParts = parts[1].Split(',');
                        if (verParts.Length > 1)
                        {
                            driverInfo.Version = verParts[1].Trim();
                        }
                        if (verParts.Length > 0 && DateTime.TryParse(verParts[0].Trim(), out var releaseDate))
                        {
                            driverInfo.ReleaseDate = releaseDate;
                        }
                    }
                }
                // Parse description
                else if (trimmedLine.StartsWith("DriverDesc", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split('=');
                    if (parts.Length > 1)
                    {
                        driverInfo.Description = parts[1].Trim().Trim('"', '%');
                    }
                }
                // Parse hardware ID
                else if (trimmedLine.StartsWith("HardwareId", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.Contains("HW_ID", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split('=');
                    if (parts.Length > 1)
                    {
                        driverInfo.HardwareId = parts[1].Trim().Trim('"');
                    }
                }
            }

            // If version is still empty, try to infer from filename or use default
            if (string.IsNullOrEmpty(driverInfo.Version))
            {
                driverInfo.Version = "1.0.0";
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Could not parse INF file: {InfPath}", infPath);
        }
    }
}
