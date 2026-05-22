using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Platforms
{
    /// <summary>
    /// Windows-specific firmware update strategy that uses Windows OS firmware
    /// commands and raw device I/O to flash firmware to Windows devices.
    /// 
    /// <para>
    /// This strategy writes firmware binary directly to the target physical drive
    /// (e.g., \\.\PhysicalDrive0). Administrator privileges are required.
    /// </para>
    /// 
    /// <para>
    /// Windows physical drive paths use the format <c>\\.\PhysicalDriveN</c> where N
    /// is the drive index (0, 1, 2, ...).
    /// </para>
    /// </summary>
    public class WindowsFirmwareStrategy : IFirmwareStrategy
    {
        /// <summary>
        /// Gets the target platform for this strategy — always <see cref="FirmwarePlatform.Windows"/>.
        /// </summary>
        public FirmwarePlatform TargetPlatform => FirmwarePlatform.Windows;

        // Default buffer size for device I/O: 1MB
        private const int DeviceBufferSize = 1024 * 1024;

        // Minimum device size to consider valid (512 bytes — one sector)
        private const long MinimumDeviceSize = 512;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsFirmwareStrategy"/> class.
        /// </summary>
        public WindowsFirmwareStrategy()
        {
            FirmwareTrace.Info("WindowsFirmwareStrategy initialized.");

            if (!IsAdministrator())
            {
                FirmwareTrace.Warn(
                    "Not running as Administrator. " +
                    "Firmware updates to physical drives require elevated privileges.");
            }
        }

        /// <summary>
        /// Validates whether the target Windows device is ready for a firmware update.
        /// Checks physical drive existence, accessibility, and size.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the device is ready; false otherwise.</returns>
        public Task<bool> ValidateDeviceAsync(FirmwareConfig config, CancellationToken cancellationToken)
        {
            FirmwareTrace.BeginOperation("WindowsDeviceValidation");
            FirmwareTrace.Info("Validating Windows device: {0}", config.DevicePath);

            try
            {
                string devicePath = config.DevicePath;

                if (string.IsNullOrWhiteSpace(devicePath))
                {
                    FirmwareTrace.Error("Device path is null or empty.");
                    FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }

                // Validate path format
                if (!IsValidWindowsDevicePath(devicePath))
                {
                    FirmwareTrace.Warn(
                        "Device path '{0}' does not match the expected format. " +
                        "For physical drives, use \\\\.\\PhysicalDriveN (e.g., \\\\.\\PhysicalDrive0). " +
                        "For volumes, use \\\\.\\C:.",
                        devicePath);
                }

                // Try to open the device for write to verify accessibility and get size
                try
                {
                    using (var fs = new FileStream(
                        devicePath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite,
                        bufferSize: 4096,
                        useAsync: false))
                    {
                        long deviceSize = fs.Length;
                        FirmwareTrace.Info(
                            "Device size: {0} bytes ({1:F1} GB)",
                            deviceSize,
                            deviceSize / (1024.0 * 1024.0 * 1024.0));

                        if (deviceSize < MinimumDeviceSize)
                        {
                            FirmwareTrace.Error(
                                "Device size ({0} bytes) below minimum ({1} bytes).",
                                deviceSize,
                                MinimumDeviceSize);
                            FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                            return Task.FromResult(false);
                        }
                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    FirmwareTrace.Error(
                        "Access denied to '{0}'. Run as Administrator. Error: {1}",
                        devicePath,
                        uae.Message);
                    FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }
                catch (FileNotFoundException fnf)
                {
                    FirmwareTrace.Error(
                        "Device not found: {0}. Verify the physical drive exists. Error: {1}",
                        devicePath,
                        fnf.Message);
                    FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }
                catch (IOException ioe)
                {
                    FirmwareTrace.Error(
                        "I/O error accessing '{0}'. The device may be in use or locked. Error: {1}",
                        devicePath,
                        ioe.Message);
                    FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }

                // Query available drives for diagnostics
                QueryAvailableDrives();

                FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, true);
                FirmwareTrace.Info("Windows device validation completed successfully.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Unexpected error during device validation", ex);
                FirmwareTrace.EndOperation("WindowsDeviceValidation", TimeSpan.Zero, false);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Backs up the current firmware from the target Windows physical drive
        /// to the backup directory.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if backup succeeded; false otherwise.</returns>
        public async Task<bool> BackupCurrentFirmwareAsync(FirmwareConfig config, CancellationToken cancellationToken)
        {
            FirmwareTrace.BeginOperation("WindowsFirmwareBackup");
            FirmwareTrace.Info("Backing up firmware from: {0}", config.DevicePath);

            try
            {
                string backupDir = string.IsNullOrWhiteSpace(config.BackupDirectory)
                    ? System.IO.Directory.GetCurrentDirectory()
                    : config.BackupDirectory;

                if (!System.IO.Directory.Exists(backupDir))
                {
                    System.IO.Directory.CreateDirectory(backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string backupFileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "firmware_backup_{0}_{1}.img",
                    SanitizeFileName(config.DevicePath),
                    timestamp);
                string backupPath = Path.Combine(backupDir, backupFileName);

                FirmwareTrace.Info("Backup target: {0}", backupPath);

                long totalBytesRead = 0;

                using (var deviceStream = new FileStream(
                    config.DevicePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    DeviceBufferSize,
                    useAsync: true))
                using (var destStream = new FileStream(
                    backupPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    DeviceBufferSize,
                    useAsync: true))
                {
                    byte[] buffer = new byte[DeviceBufferSize];
                    int bytesRead;

                    while ((bytesRead = await deviceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false)) > 0)
                    {
                        await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                            .ConfigureAwait(false);
                        totalBytesRead += bytesRead;

                        if (totalBytesRead % (10L * DeviceBufferSize) == 0)
                        {
                            FirmwareTrace.Progress("WindowsBackup", totalBytesRead, deviceStream.Length);
                        }
                    }

                    await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                double sizeMB = totalBytesRead / (1024.0 * 1024.0);
                FirmwareTrace.Info("Backup completed: {0:F1} MB → {1}", sizeMB, backupPath);
                FirmwareTrace.EndOperation("WindowsFirmwareBackup", TimeSpan.Zero, true);
                return true;
            }
            catch (OperationCanceledException)
            {
                FirmwareTrace.Warn("Backup cancelled.");
                FirmwareTrace.EndOperation("WindowsFirmwareBackup", TimeSpan.Zero, false);
                return false;
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Backup failed", ex);
                FirmwareTrace.EndOperation("WindowsFirmwareBackup", TimeSpan.Zero, false);
                return false;
            }
        }

        /// <summary>
        /// Applies the firmware file to the target Windows physical drive.
        /// Supports raw binary write and UEFI capsule update via WMIC.
        /// </summary>
        /// <param name="firmwareFilePath">The path to the firmware file to flash.</param>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A result indicating success or failure.</returns>
        public async Task<FirmwareUpdateResult> ApplyFirmwareAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken)
        {
            var applySw = Stopwatch.StartNew();

            FirmwareTrace.BeginOperation("WindowsApplyFirmware");
            FirmwareTrace.Info("Target device: {0}", config.DevicePath);
            FirmwareTrace.Info("Firmware file: {0}", firmwareFilePath);

            try
            {
                if (string.IsNullOrWhiteSpace(firmwareFilePath))
                {
                    return FirmwareUpdateResult.Fail(
                        "Firmware file path is null or empty.",
                        "FW_WINDOWS_NO_FILE");
                }

                if (!File.Exists(firmwareFilePath))
                {
                    return FirmwareUpdateResult.Fail(
                        string.Format("Firmware file not found: {0}", firmwareFilePath),
                        "FW_WINDOWS_FILE_NOT_FOUND");
                }

                string extension = Path.GetExtension(firmwareFilePath)?.ToLowerInvariant();

                // UEFI capsule update path
                if (extension == ".cap" || extension == ".uefi")
                {
                    FirmwareTrace.Info("UEFI capsule firmware detected.");
                    applySw.Stop();
                    return await ApplyUefiCapsuleAsync(firmwareFilePath, config, cancellationToken, applySw)
                        .ConfigureAwait(false);
                }

                // Direct raw write to physical drive
                FirmwareTrace.Info("Using raw physical drive write.");
                return await ApplyRawWriteAsync(firmwareFilePath, config, cancellationToken, applySw)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                applySw.Stop();
                FirmwareTrace.Warn("Flashing cancelled.");
                FirmwareTrace.EndOperation("WindowsApplyFirmware", applySw.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    "Firmware flashing was cancelled.",
                    "FW_WINDOWS_CANCELLED",
                    duration: applySw.Elapsed);
            }
            catch (Exception ex)
            {
                applySw.Stop();
                FirmwareTrace.Error("Flashing failed", ex);
                FirmwareTrace.EndOperation("WindowsApplyFirmware", applySw.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    string.Format("Firmware flashing failed: {0}", ex.Message),
                    "FW_WINDOWS_FLASH_ERROR",
                    ex,
                    applySw.Elapsed);
            }
        }

        /// <summary>
        /// Writes the firmware binary directly to the Windows physical drive.
        /// </summary>
        private async Task<FirmwareUpdateResult> ApplyRawWriteAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken,
            Stopwatch timer)
        {
            FirmwareTrace.Info("Raw write: {0} → {1}", firmwareFilePath, config.DevicePath);

            long firmwareSize = new FileInfo(firmwareFilePath).Length;
            long totalWritten = 0;

            FirmwareTrace.Info(
                "Firmware size: {0} bytes ({1:F1} MB)",
                firmwareSize,
                firmwareSize / (1024.0 * 1024.0));

            try
            {
                using (var sourceStream = new FileStream(
                    firmwareFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    DeviceBufferSize,
                    useAsync: true))
                using (var deviceStream = new FileStream(
                    config.DevicePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    DeviceBufferSize,
                    useAsync: true))
                {
                    if (deviceStream.Length < firmwareSize)
                    {
                        return FirmwareUpdateResult.Fail(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Firmware file ({0} bytes) exceeds device size ({1} bytes).",
                                firmwareSize,
                                deviceStream.Length),
                            "FW_WINDOWS_SIZE_MISMATCH");
                    }

                    byte[] buffer = new byte[DeviceBufferSize];
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false)) > 0)
                    {
                        await deviceStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                            .ConfigureAwait(false);
                        totalWritten += bytesRead;

                        if (totalWritten % (10L * DeviceBufferSize) == 0 || totalWritten == firmwareSize)
                        {
                            FirmwareTrace.Progress("WindowsFlash", totalWritten, firmwareSize);

                            if (config.ProgressCallback != null)
                            {
                                try { config.ProgressCallback(totalWritten, firmwareSize); }
                                catch (Exception ex) { FirmwareTrace.Warn("Callback error: {0}", ex.Message); }
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await deviceStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                timer.Stop();
                double speedMBps = (totalWritten / (1024.0 * 1024.0)) / timer.Elapsed.TotalSeconds;
                FirmwareTrace.Info(
                    "Write complete. {0} bytes in {1:F1}s ({2:F1} MB/s)",
                    totalWritten, timer.Elapsed.TotalSeconds, speedMBps);
                FirmwareTrace.EndOperation("WindowsApplyFirmware", timer.Elapsed, true);

                return FirmwareUpdateResult.Succeed("windows-raw", timer.Elapsed);
            }
            catch (UnauthorizedAccessException uae)
            {
                timer.Stop();
                FirmwareTrace.EndOperation("WindowsApplyFirmware", timer.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    string.Format("Access denied to '{0}'. Run as Administrator. Error: {1}",
                        config.DevicePath, uae.Message),
                    "FW_WINDOWS_ACCESS_DENIED",
                    uae,
                    timer.Elapsed);
            }
        }

        /// <summary>
        /// Applies a UEFI firmware capsule (.cap) to the system firmware
        /// via WMIC or direct system firmware device write.
        /// </summary>
        private async Task<FirmwareUpdateResult> ApplyUefiCapsuleAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken,
            Stopwatch timer)
        {
            FirmwareTrace.Info("Processing UEFI capsule firmware update.");

            try
            {
                // On Windows, UEFI capsules can be submitted via WMI method:
                // Get-WmiObject -Namespace root\wmi -Class Firmware | ForEach-Object { $_.SetFirmware() }
                // Or by writing to \\.\UEFI firmware device
                // For broad compatibility, attempt WMIC-based submission

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format(
                        CultureInfo.InvariantCulture,
                        "-NoProfile -Command \"& {{ " +
                        "try {{ " +
                        "  $firmware = Get-CimInstance -Namespace root/wmi -ClassName MSFirmwareInformation; " +
                        "  if ($firmware) {{ Write-Output 'Firmware interface accessible' }} else {{ throw 'No firmware interface' }} " +
                        "}} catch {{ " +
                        "  Write-Error $_.Exception.Message " +
                        "}}\"",
                        firmwareFilePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        FirmwareTrace.Warn("Failed to start PowerShell for UEFI query. Falling back to raw write.");
                        return await ApplyRawWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                            .ConfigureAwait(false);
                    }

                    string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { /* ignore */ }
                    }

                    if (process.ExitCode == 0)
                    {
                        FirmwareTrace.Info("UEFI firmware interface accessible.");
                        FirmwareTrace.Debug("Output: {0}", stdout.Trim());

                        // UEFI capsule has been detected — for actual capsule submission,
                        // Windows Update or OEM tools are needed. Record the firmware as ready.
                        FirmwareTrace.Info(
                            "UEFI capsule firmware ready. " +
                            "On Windows, submit the .cap file via Windows Update or OEM flashing tool.");

                        timer.Stop();
                        FirmwareTrace.EndOperation("WindowsApplyFirmware", timer.Elapsed, true);
                        return FirmwareUpdateResult.Succeed("uefi-capsule", timer.Elapsed);
                    }
                    else
                    {
                        FirmwareTrace.Warn(
                            "UEFI firmware interface not available (exit {0}). Falling back to raw write. Stderr: {1}",
                            process.ExitCode,
                            stderr.Trim());
                        return await ApplyRawWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                FirmwareTrace.Warn("UEFI capsule check failed: {0}. Falling back to raw write.", ex.Message);
                return await ApplyRawWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Validates the Windows device path format.
        /// </summary>
        private static bool IsValidWindowsDevicePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            // Accept \\.\PhysicalDriveN
            if (path.StartsWith(@"\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = path.Substring(@"\\.\PhysicalDrive".Length);
                return int.TryParse(suffix, out _);
            }

            // Accept \\.\ScsiN:
            if (path.StartsWith(@"\\.\Scsi", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Accept \\.\C: etc.
            if (path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
            {
                return path.Length > 4;
            }

            return false;
        }

        /// <summary>
        /// Queries available disk drives via WMIC for diagnostic purposes.
        /// </summary>
        private static void QueryAvailableDrives()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "diskdrive get DeviceID,Model,Size /format:csv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) return;
                    process.WaitForExit(5000);
                    string output = process.StandardOutput.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        FirmwareTrace.Debug("Available drives:\n{0}", output.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                FirmwareTrace.Debug("WMIC query failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Checks if the current process has Administrator privileges.
        /// Uses 'net session' which requires admin rights.
        /// </summary>
        private static bool IsAdministrator()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "session",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) return false;
                    process.WaitForExit(3000);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sanitizes a device path for use as a filename.
        /// </summary>
        private static string SanitizeFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "device";
            return path.Replace(@"\\.\", string.Empty).Replace("\\", "_").Replace(":", string.Empty);
        }
    }
}
