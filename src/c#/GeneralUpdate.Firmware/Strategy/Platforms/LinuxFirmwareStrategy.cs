using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Strategy.Connections;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Platforms
{
    /// <summary>
    /// Linux-specific firmware update strategy that leverages vela's firmware upgrade
    /// capabilities for dual A/B slot flash operations on embedded Linux devices.
    /// 
    /// <para>
    /// This strategy supports two update modes:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Vela FlashPack mode</b> — For structured <c>.fpk</c> firmware packages,
    ///   uses the vela-flashpack engine for A/B slot management.</description></item>
    ///   <item><description><b>Direct write mode</b> — For bare firmware binaries (<c>.bin</c>, <c>.img</c>),
    ///   writes directly to the target block device.</description></item>
    /// </list>
    /// 
    /// <para>
    /// The strategy auto-detects vela availability on the system at initialization time
    /// and selects the appropriate mode.
    /// </para>
    /// </summary>
    public class LinuxFirmwareStrategy : IFirmwareStrategy
    {
        /// <summary>
        /// Gets the target platform for this strategy — always <see cref="FirmwarePlatform.Linux"/>.
        /// </summary>
        public FirmwarePlatform TargetPlatform => FirmwarePlatform.Linux;

        /// <summary>
        /// Gets whether the vela native library (libvela_ffi.so) is available on this system.
        /// </summary>
        public bool IsVelaAvailable { get; }

        // Default buffer size for block device I/O: 1MB
        private const int BlockDeviceBufferSize = 1024 * 1024;

        // Engine handle for P/Invoke calls (null if vela is not available)
        private IntPtr _velaHandle = IntPtr.Zero;

        // Minimum device size to consider valid (512 bytes — one sector)
        private const long MinimumDeviceSize = 512;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinuxFirmwareStrategy"/> class.
        /// Detects vela native library availability at construction time.
        /// </summary>
        public LinuxFirmwareStrategy()
        {
            IsVelaAvailable = VelaNativeMethods.IsAvailable;
            FirmwareTrace.Info(
                "LinuxFirmwareStrategy initialized. Vela native library available: {0}",
                IsVelaAvailable);
        }

        /// <summary>
        /// Validates whether the target device is ready for a firmware update.
        /// Checks device existence, block device type, and write permissions.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the device is ready; false otherwise.</returns>
        public Task<bool> ValidateDeviceAsync(FirmwareConfig config, CancellationToken cancellationToken)
        {
            FirmwareTrace.BeginOperation("LinuxDeviceValidation");
            FirmwareTrace.Info("Validating Linux device: {0}", config.DevicePath);

            try
            {
                string devicePath = config.DevicePath;

                if (string.IsNullOrWhiteSpace(devicePath))
                {
                    FirmwareTrace.Error("Device path is null or empty.");
                    FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }

                // Check if the device exists
                if (!File.Exists(devicePath) && !System.IO.Directory.Exists(devicePath))
                {
                    // On Linux, block devices may not appear as regular files.
                    // Try to check via /dev or /sys/block path.
                    FirmwareTrace.Warn(
                        "Device path '{0}' does not exist as a regular file. " +
                        "It may still be valid as a block device. " +
                        "Ensure you have the correct path (e.g., /dev/mmcblk0, /dev/sda).",
                        devicePath);
                }

                // Attempt to open the device for write to check permissions
                try
                {
                    using (var fs = new FileStream(
                        devicePath,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        useAsync: false))
                    {
                        long deviceSize = fs.Length;
                        FirmwareTrace.Info("Device size: {0} bytes ({1:F1} MB)", deviceSize, deviceSize / (1024.0 * 1024.0));

                        if (deviceSize < MinimumDeviceSize)
                        {
                            FirmwareTrace.Error(
                                "Device size ({0} bytes) is below the minimum ({1} bytes).",
                                deviceSize,
                                MinimumDeviceSize);
                            FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                            return Task.FromResult(false);
                        }
                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    FirmwareTrace.Error(
                        "Permission denied accessing device '{0}'. " +
                        "Firmware update requires root privileges or appropriate device permissions. " +
                        "Error: {1}",
                        devicePath,
                        uae.Message);
                    FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }
                catch (FileNotFoundException fnf)
                {
                    FirmwareTrace.Error("Device not found: {0}. Error: {1}", devicePath, fnf.Message);
                    FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }
                catch (DirectoryNotFoundException dnf)
                {
                    FirmwareTrace.Error("Device directory not found: {0}. Error: {1}", devicePath, dnf.Message);
                    FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                    return Task.FromResult(false);
                }

                // Check vela availability if using .fpk format
                if (IsVelaAvailable)
                {
                    FirmwareTrace.Info("Vela native library detected — will use A/B slot management via P/Invoke.");
                }
                else
                {
                    FirmwareTrace.Warn(
                        "Vela native library not detected. Will use direct block device write mode. " +
                        "A/B slot management is not available without vela.");
                }

                FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, true);
                FirmwareTrace.Info("Linux device validation completed successfully.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Unexpected error during device validation", ex);
                FirmwareTrace.EndOperation("LinuxDeviceValidation", TimeSpan.Zero, false);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Backs up the current firmware from the target device to the backup directory.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if backup succeeded; false otherwise.</returns>
        public async Task<bool> BackupCurrentFirmwareAsync(FirmwareConfig config, CancellationToken cancellationToken)
        {
            FirmwareTrace.BeginOperation("LinuxFirmwareBackup");
            FirmwareTrace.Info("Backing up firmware from device: {0}", config.DevicePath);

            try
            {
                string backupDir = string.IsNullOrWhiteSpace(config.BackupDirectory)
                    ? System.IO.Directory.GetCurrentDirectory()
                    : config.BackupDirectory;

                if (!System.IO.Directory.Exists(backupDir))
                {
                    System.IO.Directory.CreateDirectory(backupDir);
                    FirmwareTrace.Debug("Created backup directory: {0}", backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string backupFileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "firmware_backup_{0}_{1}.img",
                    Path.GetFileName(config.DevicePath) ?? "device",
                    timestamp);
                string backupPath = Path.Combine(backupDir, backupFileName);

                FirmwareTrace.Info("Backup target path: {0}", backupPath);

                long totalBytesRead = 0;

                using (var sourceStream = new FileStream(
                    config.DevicePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BlockDeviceBufferSize,
                    useAsync: true))
                using (var destStream = new FileStream(
                    backupPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BlockDeviceBufferSize,
                    useAsync: true))
                {
                    byte[] buffer = new byte[BlockDeviceBufferSize];
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false)) > 0)
                    {
                        await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                            .ConfigureAwait(false);
                        totalBytesRead += bytesRead;

                        if (totalBytesRead % (10 * BlockDeviceBufferSize) == 0)
                        {
                            FirmwareTrace.Progress("Backup", totalBytesRead, sourceStream.Length);

                            // Fire progress callback for the backup stage
                            FireProgress(config, FirmwareUpdateStage.BackingUp,
                                sourceStream.Length > 0 ? (float)totalBytesRead / sourceStream.Length * 100f : 0f,
                                5f + (sourceStream.Length > 0 ? (float)totalBytesRead / sourceStream.Length * 15f : 0f),
                                string.Format(CultureInfo.InvariantCulture, "Backing up... {0}/{1} MB",
                                    totalBytesRead / (1024 * 1024), sourceStream.Length / (1024 * 1024)));
                        }
                    }

                    await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                FirmwareTrace.Info(
                    "Backup completed. {0} bytes written to {1}",
                    totalBytesRead,
                    backupPath);
                config.LastBackupPath = backupPath;
                FirmwareTrace.EndOperation("LinuxFirmwareBackup", TimeSpan.Zero, true);
                return true;
            }
            catch (OperationCanceledException)
            {
                FirmwareTrace.Warn("Firmware backup was cancelled.");
                FirmwareTrace.EndOperation("LinuxFirmwareBackup", TimeSpan.Zero, false);
                return false;
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Firmware backup failed", ex);
                FirmwareTrace.EndOperation("LinuxFirmwareBackup", TimeSpan.Zero, false);
                return false;
            }
        }

        /// <summary>
        /// Applies the firmware file to the target Linux block device.
        /// Supports both vela FlashPack (.fpk) and raw binary formats.
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

            FirmwareTrace.BeginOperation("LinuxApplyFirmware");
            FirmwareTrace.Info("Flashing firmware to device: {0}", config.DevicePath);
            FirmwareTrace.Info("Firmware file: {0}", firmwareFilePath);

            try
            {
                if (string.IsNullOrWhiteSpace(firmwareFilePath))
                {
                    return FirmwareUpdateResult.Fail(
                        "Firmware file path is null or empty.",
                        "FW_LINUX_NO_FILE");
                }

                if (!File.Exists(firmwareFilePath))
                {
                    return FirmwareUpdateResult.Fail(
                        string.Format("Firmware file not found: {0}", firmwareFilePath),
                        "FW_LINUX_FILE_NOT_FOUND");
                }

                // Determine flashing mode based on file extension and vela availability
                string extension = Path.GetExtension(firmwareFilePath)?.ToLowerInvariant();
                bool isFlashPack = extension == ".fpk";

                if (isFlashPack && IsVelaAvailable)
                {
                    FirmwareTrace.Info("Using vela FlashPack mode for .fpk firmware");
                    applySw.Stop();
                    return await ApplyVelaFlashPackAsync(firmwareFilePath, config, cancellationToken, applySw)
                        .ConfigureAwait(false);
                }

                if (isFlashPack && !IsVelaAvailable)
                {
                    return FirmwareUpdateResult.Fail(
                        "Firmware file is in vela FlashPack (.fpk) format, but vela is not installed on this system. " +
                        "Install vela-core or use a raw firmware binary (.bin, .img).",
                        "FW_LINUX_VELA_NOT_FOUND");
                }

                // Direct block device write mode
                FirmwareTrace.Info("Using direct block device write mode");
                applySw.Stop();
                return await ApplyDirectWriteAsync(firmwareFilePath, config, cancellationToken, applySw)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                applySw.Stop();
                FirmwareTrace.Warn("Firmware flashing was cancelled.");
                FirmwareTrace.EndOperation("LinuxApplyFirmware", applySw.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    "Firmware flashing was cancelled.",
                    "FW_LINUX_CANCELLED",
                    duration: applySw.Elapsed);
            }
            catch (Exception ex)
            {
                applySw.Stop();
                FirmwareTrace.Error("Firmware flashing failed", ex);
                FirmwareTrace.EndOperation("LinuxApplyFirmware", applySw.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    string.Format("Firmware flashing failed: {0}", ex.Message),
                    "FW_LINUX_FLASH_ERROR",
                    ex,
                    applySw.Elapsed);
            }
        }

        /// <summary>
        /// Writes the firmware binary to the target device using the configured
        /// connection type (BlockDevice via FileStream, or other IConnection implementations).
        /// </summary>
        private async Task<FirmwareUpdateResult> ApplyDirectWriteAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken,
            Stopwatch timer)
        {
            FirmwareTrace.Info(
                "Starting firmware write via {0}: {1} -> {2}",
                config.Connection.Type,
                firmwareFilePath,
                config.DevicePath ?? config.Connection.ToString());

            long firmwareSize = new FileInfo(firmwareFilePath).Length;
            FirmwareTrace.Info("Firmware size: {0} bytes ({1:F1} MB)", firmwareSize, firmwareSize / (1024.0 * 1024.0));

            IConnection connection = null;

            try
            {
                // Create the appropriate connection based on config.Connection.Type
                connection = Connections.ConnectionFactory.Create(config.Connection);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                // === Chunked streaming write with progress ===
                // Avoids File.ReadAllBytes — large firmware files won't OOM.
                // Each 1 MB chunk is read, written, and reported independently.
                var chunkBuffer = new byte[1024 * 1024]; // 1 MB chunks
                long totalWritten = 0;
                var writeSw = System.Diagnostics.Stopwatch.StartNew();
                long lastReportBytes = 0;
                long lastReportMs = 0;
                var speedSamples = new System.Collections.Generic.Queue<double>(4);

                using (var fileStream = new FileStream(
                    firmwareFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1024 * 1024, useAsync: true))
                {
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(
                        chunkBuffer, 0, chunkBuffer.Length, cancellationToken)
                        .ConfigureAwait(false)) > 0)
                    {
                        // Write this chunk to the device
                        if (bytesRead == chunkBuffer.Length)
                        {
                            await connection.WriteAsync(chunkBuffer, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            // Last chunk: copy to correctly-sized array
                            byte[] lastChunk = new byte[bytesRead];
                            Array.Copy(chunkBuffer, 0, lastChunk, 0, bytesRead);
                            await connection.WriteAsync(lastChunk, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        totalWritten += bytesRead;

                        // Report progress at ~500 ms intervals
                        long elapsedMs = writeSw.ElapsedMilliseconds;
                        if (elapsedMs - lastReportMs >= 500 || totalWritten == firmwareSize)
                        {
                            double deltaBytes = totalWritten - lastReportBytes;
                            double deltaSecs = (elapsedMs - lastReportMs) / 1000.0;
                            double instantSpeed = deltaSecs > 0 ? deltaBytes / deltaSecs : 0;

                            speedSamples.Enqueue(instantSpeed);
                            if (speedSamples.Count > 3) speedSamples.Dequeue();
                            double avgSpeed = 0;
                            foreach (var s in speedSamples) avgSpeed += s;
                            avgSpeed = speedSamples.Count > 0 ? avgSpeed / speedSamples.Count : instantSpeed;

                            TimeSpan eta = firmwareSize > 0 && avgSpeed > 0
                                ? TimeSpan.FromSeconds((firmwareSize - totalWritten) / avgSpeed)
                                : TimeSpan.Zero;

                            float stagePct = firmwareSize > 0
                                ? (float)totalWritten / firmwareSize * 100f
                                : 0f;

                            FireProgress(config, FirmwareUpdateStage.Flashing,
                                stagePct,
                                80f + (stagePct * 0.20f),
                                string.Format(CultureInfo.InvariantCulture,
                                    "Flashing... {0}/{1} MB | {2:F1} MB/s",
                                    totalWritten / (1024 * 1024),
                                    firmwareSize / (1024 * 1024),
                                    avgSpeed / (1024 * 1024)));

                            // Fire download-style callback (bytes, total, speed, eta) for flashing too
                            if (config.OnDownloadProgress != null)
                            {
                                try { config.OnDownloadProgress(totalWritten, firmwareSize, avgSpeed, eta); }
                                catch (Exception ex) { FirmwareTrace.Warn("OnDownloadProgress error: {0}", ex.Message); }
                            }

                            // Legacy callback
#pragma warning disable 618
                            if (config.ProgressCallback != null)
                            {
                                try { config.ProgressCallback(totalWritten, firmwareSize); }
                                catch (Exception ex) { FirmwareTrace.Warn("Legacy callback error: {0}", ex.Message); }
                            }
#pragma warning restore 618

                            lastReportBytes = totalWritten;
                            lastReportMs = elapsedMs;
                        }
                    }
                }

                writeSw.Stop();

                // === Write-verify: read back and compare with source ===
                bool verified = true;
                if (config.EnableWriteVerify)
                {
                    verified = await VerifyWrittenDataAsync(
                        firmwareFilePath, config, firmwareSize, cancellationToken)
                        .ConfigureAwait(false);

                    if (!verified)
                    {
                        timer.Stop();
                        FirmwareTrace.EndOperation("LinuxApplyFirmware", timer.Elapsed, false);
                        return FirmwareUpdateResult.Fail(
                            "Write verification failed: data read back from device does not match source.",
                            "FW_LINUX_VERIFY_FAILED",
                            duration: timer.Elapsed);
                    }
                }

                timer.Stop();
                double speedMBps = (totalWritten / (1024.0 * 1024.0)) / writeSw.Elapsed.TotalSeconds;
                FirmwareTrace.Info(
                    "Firmware written successfully. {0} bytes in {1:F1}s ({2:F1} MB/s)",
                    totalWritten,
                    writeSw.Elapsed.TotalSeconds,
                    speedMBps);
                FirmwareTrace.EndOperation("LinuxApplyFirmware", timer.Elapsed, true);

                return FirmwareUpdateResult.Succeed(config.Connection.Type.ToString(), timer.Elapsed);
            }
            catch (UnauthorizedAccessException uae)
            {
                timer.Stop();
                FirmwareTrace.EndOperation("LinuxApplyFirmware", timer.Elapsed, false);
                return FirmwareUpdateResult.Fail(
                    string.Format("Access denied. Error: {0}", uae.Message),
                    "FW_LINUX_ACCESS_DENIED",
                    uae,
                    timer.Elapsed);
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Applies a vela FlashPack (.fpk) firmware package to the device using the
        /// vela native library (P/Invoke) for A/B slot management.
        /// </summary>
        private async Task<FirmwareUpdateResult> ApplyVelaFlashPackAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken,
            Stopwatch timer)
        {
            FirmwareTrace.Info("Using vela native library (P/Invoke) for .fpk firmware");

            try
            {
                // Initialize vela engine
                _velaHandle = VelaNativeMethods.vela_flash_init(null);
                if (_velaHandle == IntPtr.Zero)
                {
                    string err = VelaNativeMethods.GetLastError() ?? "unknown error";
                    FirmwareTrace.Error("Failed to initialize vela engine: {0}", err);
                    return await ApplyDirectWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                        .ConfigureAwait(false);
                }

                try
                {
                    // Validate device via vela
                    int validateResult = VelaNativeMethods.vela_flash_validate_device(
                        _velaHandle, config.DevicePath);
                    if (validateResult != 0)
                    {
                        string err = VelaNativeMethods.GetLastError() ?? "device check failed";
                        FirmwareTrace.Error("Vela device validation failed: {0}", err);
                        return FirmwareUpdateResult.Fail(
                            string.Format("Vela device validation failed: {0}", err),
                            "FW_LINUX_VELA_VALIDATE");
                    }

                    // Flash the .fpk via vela native library
                    FirmwareTrace.Info(
                        "Vela flash: fpk={0}, device={1}",
                        firmwareFilePath,
                        config.DevicePath);

                    ulong bytesWritten = await Task.Run(() =>
                        VelaNativeMethods.vela_flash_write_fpk(
                            _velaHandle,
                            firmwareFilePath,
                            config.DevicePath),
                        cancellationToken).ConfigureAwait(false);

                    if (bytesWritten == 0)
                    {
                        string err = VelaNativeMethods.GetLastError() ?? "flash returned 0 bytes";
                        FirmwareTrace.Error("Vela flash failed: {0}", err);

                        timer.Stop();
                        FirmwareTrace.EndOperation("LinuxApplyFirmware", timer.Elapsed, false);
                        return FirmwareUpdateResult.Fail(
                            string.Format("Vela flash failed: {0}", err),
                            "FW_LINUX_VELA_FLASH_ERROR",
                            duration: timer.Elapsed);
                    }

                    // Switch to the alternate slot after successful flash
                    int switchResult = VelaNativeMethods.vela_flash_switch_slot(_velaHandle);
                    if (switchResult == 0)
                    {
                        string activeSlot = VelaNativeMethods.GetActiveSlot(_velaHandle);
                        FirmwareTrace.Info(
                            "Slot switched successfully. New active slot: {0}",
                            activeSlot ?? "unknown");
                    }
                    else
                    {
                        FirmwareTrace.Warn(
                            "Slot switch reported failure (code: {0}). The system may need manual slot selection.",
                            switchResult);
                    }

                    FirmwareTrace.Info(
                        "Vela flash completed successfully. {0} bytes written.",
                        bytesWritten);

                    timer.Stop();
                    FirmwareTrace.EndOperation("LinuxApplyFirmware", timer.Elapsed, true);
                    return FirmwareUpdateResult.Succeed("vela-pinvoke", timer.Elapsed);
                }
                finally
                {
                    // Always shut down the engine
                    if (_velaHandle != IntPtr.Zero)
                    {
                        VelaNativeMethods.vela_flash_shutdown(_velaHandle);
                        _velaHandle = IntPtr.Zero;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                FirmwareTrace.Warn("Vela flash operation was cancelled.");
                throw;
            }
            catch (DllNotFoundException)
            {
                FirmwareTrace.Warn(
                    "Vela native library not found. Falling back to direct block device write.");
                return await ApplyDirectWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Vela native flash failed", ex);
                FirmwareTrace.Warn("Falling back to direct block device write.");
                return await ApplyDirectWriteAsync(firmwareFilePath, config, cancellationToken, timer)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Safely fires <see cref="FirmwareConfig.OnProgress"/> during backup or flashing.
        /// </summary>
        private static void FireProgress(
            FirmwareConfig config,
            FirmwareUpdateStage stage,
            float stagePct,
            float overallPct,
            string statusText)
        {
            var callback = config?.OnProgress;
            if (callback == null) return;
            try
            {
                callback(new FirmwareProgressInfo
                {
                    Stage = stage,
                    StageProgressPercent = stagePct,
                    OverallProgressPercent = overallPct,
                    StatusText = statusText
                });
            }
            catch (Exception ex)
            {
                FirmwareTrace.Warn("Progress callback threw an exception (ignored): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Verifies that data written to the device matches the source firmware file.
        /// Reads back from the device in chunks and compares with the source.
        /// Reports progress via <see cref="FirmwareConfig.OnProgress"/>.
        /// </summary>
        private async Task<bool> VerifyWrittenDataAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            FireProgress(config, FirmwareUpdateStage.VerifyingWrite, 0, 100f, "Verifying written data...");
            FirmwareTrace.Info("Starting write-verify. Expected size: {0} bytes", expectedSize);

            try
            {
                var verifySw = System.Diagnostics.Stopwatch.StartNew();
                var sourceBuffer = new byte[1024 * 1024];
                var deviceBuffer = new byte[1024 * 1024];
                long totalVerified = 0;
                long lastReportMs = 0;

                using (var sourceStream = new FileStream(
                    firmwareFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1024 * 1024, useAsync: true))
                using (var deviceStream = new FileStream(
                    config.DevicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                    bufferSize: 1024 * 1024, useAsync: true))
                {
                    while (totalVerified < expectedSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int sourceBytes = await sourceStream.ReadAsync(
                            sourceBuffer, 0, sourceBuffer.Length, cancellationToken)
                            .ConfigureAwait(false);

                        if (sourceBytes == 0) break;

                        int devicePos = 0;
                        while (devicePos < sourceBytes)
                        {
                            int deviceBytes = await deviceStream.ReadAsync(
                                deviceBuffer, devicePos, sourceBytes - devicePos, cancellationToken)
                                .ConfigureAwait(false);

                            if (deviceBytes == 0)
                            {
                                FirmwareTrace.Error("Device read returned 0 bytes at offset {0}", totalVerified + devicePos);
                                return false;
                            }
                            devicePos += deviceBytes;
                        }

                        // Compare
                        for (int i = 0; i < sourceBytes; i++)
                        {
                            if (sourceBuffer[i] != deviceBuffer[i])
                            {
                                long badOffset = totalVerified + i;
                                FirmwareTrace.Error(
                                    "Write-verify mismatch at byte {0}: expected 0x{1:X2}, got 0x{2:X2}",
                                    badOffset, sourceBuffer[i], deviceBuffer[i]);
                                return false;
                            }
                        }

                        totalVerified += sourceBytes;

                        long elapsedMs = verifySw.ElapsedMilliseconds;
                        if (elapsedMs - lastReportMs >= 500 || totalVerified >= expectedSize)
                        {
                            float pct = expectedSize > 0 ? (float)totalVerified / expectedSize * 100f : 0f;
                            FireProgress(config, FirmwareUpdateStage.VerifyingWrite,
                                pct, 100f,
                                string.Format(CultureInfo.InvariantCulture,
                                    "Verifying... {0}/{1} MB",
                                    totalVerified / (1024 * 1024),
                                    expectedSize / (1024 * 1024)));
                            lastReportMs = elapsedMs;
                        }
                    }
                }

                verifySw.Stop();
                double speedMBps = (totalVerified / (1024.0 * 1024.0)) / verifySw.Elapsed.TotalSeconds;
                FirmwareTrace.Info(
                    "Write-verify passed. {0} bytes verified in {1:F1}s ({2:F1} MB/s)",
                    totalVerified, verifySw.Elapsed.TotalSeconds, speedMBps);

                FireProgress(config, FirmwareUpdateStage.VerifyingWrite, 100f, 100f, "Verification passed.");
                return true;
            }
            catch (OperationCanceledException)
            {
                FirmwareTrace.Warn("Write-verify cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Write-verify failed with exception", ex);
                return false;
            }
        }
    }
}
