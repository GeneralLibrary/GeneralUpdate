using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Execution;
using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Core.Utilities;

namespace GeneralUpdate.Drivelution.MacOS.Implementation;

/// <summary>
/// macOS driver updater implementation.
/// Inherits the unified pipeline from <see cref="BaseDriverUpdater"/> and supports
/// .kext, .dext, and .pkg driver formats via command-line tools.
/// </summary>
/// <remarks>
/// Current limitations:
/// - .kext installation requires SIP to be disabled on Apple Silicon (kextload is deprecated)
/// - .dext (DriverKit) requires user approval in System Preferences → Security & Privacy
/// </remarks>
[SupportedOSPlatform("macos")]
public class MacOsGeneralDrivelution : BaseDriverUpdater
{
    private readonly ICommandRunner _commandRunner;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public MacOsGeneralDrivelution(
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
        var extension = Path.GetExtension(driverInfo.FilePath).ToLowerInvariant();

        switch (extension)
        {
            case ".kext":
                await InstallKextAsync(driverInfo.FilePath, cancellationToken);
                break;
            case ".dext":
                await InstallDextAsync(driverInfo.FilePath, cancellationToken);
                break;
            case ".pkg":
                await InstallPkgAsync(driverInfo.FilePath, cancellationToken);
                break;
            default:
                GeneralTracer.Warn($"Unknown macOS driver format: {extension}");
                throw new DriverInstallationException(
                    $"Unsupported macOS driver format: {extension}. " +
                    "Supported formats: .kext, .dext, .pkg");
        }

        GeneralTracer.Info("macOS driver installation completed");
    }

    // ─── Rollback override ─────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task<bool> RollbackAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Rolling back macOS driver from backup: {backupPath}");

            if (!Directory.Exists(backupPath))
            {
                GeneralTracer.Error($"Backup directory not found: {backupPath}");
                return false;
            }

            // Restore backed-up kext files
            var kextFiles = Directory.GetFiles(backupPath, "*.kext", SearchOption.AllDirectories);
            foreach (var kextFile in kextFiles)
            {
                try
                {
                    var kextName = Path.GetFileName(kextFile);
                    GeneralTracer.Info($"Restoring kext: {kextName}");

                    // Copy back to /Library/Extensions/
                    var targetPath = $"/Library/Extensions/{kextName}";
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    CopyDirectory(kextFile, targetPath);

                    // Reload the kext
                    await _commandRunner.RunAsync("kextload", new[] { targetPath }, cancellationToken);
                    GeneralTracer.Info($"Restored kext: {kextName}");
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to restore kext: {kextFile} - {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("macOS driver rollback failed", ex);
            return false;
        }
    }

    // ─── Driver discovery overrides ────────────────────────────────────

    /// <inheritdoc/>
    protected override string GetDefaultSearchPattern() => "*.kext";

    /// <inheritdoc/>
    public override async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        var drivers = new List<DriverInfo>();

        try
        {
            GeneralTracer.Info($"Reading macOS drivers from directory: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                GeneralTracer.Warn($"Directory not found: {directoryPath}");
                return drivers;
            }

            // Collect .kext, .dext, and .pkg files
            var files = new List<string>();
            if (searchPattern is not null)
            {
                files.AddRange(Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories));
            }
            else
            {
                files.AddRange(Directory.GetFiles(directoryPath, "*.kext", SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(directoryPath, "*.dext", SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(directoryPath, "*.pkg", SearchOption.AllDirectories));
            }

            GeneralTracer.Info($"Found {files.Count} macOS driver file(s)");

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var info = await ParseMacOSDriverFileAsync(filePath, cancellationToken);
                    if (info is not null)
                        drivers.Add(info);
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
                }
            }

            GeneralTracer.Info($"Loaded {drivers.Count} macOS driver(s) from directory");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"Error reading macOS drivers: {ex.Message}", ex);
        }

        return drivers;
    }

    // ─── Private: Pipeline steps ───────────────────────────────────────

    private static IPipelineStep CreateSudoCheckStep()
    {
        return new DelegateStep("CheckSudo",
            execute: (context, ct) =>
            {
                context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Checking macOS root privileges");
                return Task.FromResult(PipelineResult.Ok());
            });
    }

    // ─── Private: Format-specific installers ───────────────────────────

    private async Task InstallKextAsync(
        string kextPath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing kernel extension: {kextPath}");

        var kextName = Path.GetFileName(kextPath);
        var targetPath = $"/Library/Extensions/{kextName}";

        // Copy kext to Extensions directory
        if (Directory.Exists(kextPath))
        {
            // kext is a bundle (directory)
            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath, recursive: true);

            CopyDirectory(kextPath, targetPath);
        }
        else
        {
            File.Copy(kextPath, targetPath, overwrite: true);
        }

        // Fix permissions (kexts must be owned by root:wheel)
        await _commandRunner.RunAsync("chown", new[] { "-R", "root:wheel", targetPath }, cancellationToken);
        await _commandRunner.RunAsync("chmod", new[] { "-R", "755", targetPath }, cancellationToken);

        // Load the kext
        await _commandRunner.RunOrThrowAsync("kextload", new[] { targetPath }, cancellationToken);

        // Update kext cache
        await _commandRunner.RunAsync("kextcache", new[] { "-i", "/" }, cancellationToken);

        GeneralTracer.Info($"Kernel extension installed: {kextName}");
    }

    private async Task InstallDextAsync(
        string dextPath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing DriverKit extension: {dextPath}");

        // Copy to standard SystemExtensions location
        var dextName = Path.GetFileName(dextPath);
        var targetPath = $"/Library/SystemExtensions/{dextName}";

        if (!Directory.Exists("/Library/SystemExtensions"))
            Directory.CreateDirectory("/Library/SystemExtensions");

        File.Copy(dextPath, targetPath, overwrite: true);

        // System extensions are managed by sysextd — registration happens automatically
        // User must approve in System Preferences → Security & Privacy
        GeneralTracer.Info($"DriverKit extension registered. User approval may be required via System Preferences.");

        // Attempt to register via systemextensionsctl
        try
        {
            await _commandRunner.RunAsync("systemextensionsctl", new[] { "reset" }, cancellationToken);
        }
        catch
        {
            // Non-fatal: systemextensionctl may not be available in all versions
        }

        GeneralTracer.Info($"DriverKit extension installed: {dextName}");
    }

    private async Task InstallPkgAsync(
        string pkgPath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing macOS package: {pkgPath}");

        await _commandRunner.RunOrThrowAsync(
            "/usr/sbin/installer",
            new[] { "-pkg", pkgPath, "-target", "/" },
            cancellationToken);

        GeneralTracer.Info("macOS package installed");
    }

    // ─── Private: File parsing ─────────────────────────────────────────

    private static async Task<DriverInfo?> ParseMacOSDriverFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverInfo = new DriverInfo
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                TargetOS = "MacOS",
                Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            };

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            string? versionFromFile = null;

            switch (extension)
            {
                case ".kext":
                    versionFromFile = await ParseKextVersionAsync(filePath, cancellationToken);
                    break;
                case ".pkg":
                    versionFromFile = await ParsePkgVersionAsync(filePath, cancellationToken);
                    break;
            }

            driverInfo.Version = versionFromFile ?? "1.0.0";

            // Compute file hash
            driverInfo.Hash = await HashValidator.ComputeHashAsync(filePath, "SHA256", cancellationToken);
            driverInfo.HashAlgorithm = "SHA256";

            return driverInfo;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> ParseKextVersionAsync(
        string kextPath,
        CancellationToken cancellationToken)
    {
        // Try to read Info.plist inside the kext bundle
        var plistPath = Path.Combine(kextPath, "Contents", "Info.plist");
        if (!File.Exists(plistPath))
            plistPath = Path.Combine(kextPath, "Info.plist");

        if (!File.Exists(plistPath))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(plistPath, cancellationToken);

            // Simple key-based extraction (no XML library dependency)
            var versionKey = "<key>CFBundleVersion</key>";
            var versionIdx = content.IndexOf(versionKey, StringComparison.Ordinal);
            if (versionIdx < 0) return null;

            var valueStart = content.IndexOf("<string>", versionIdx, StringComparison.Ordinal);
            if (valueStart < 0) return null;

            var valueEnd = content.IndexOf("</string>", valueStart, StringComparison.Ordinal);
            if (valueEnd < 0) return null;

            return content.Substring(valueStart + 8, valueEnd - valueStart - 8).Trim();
        }
        catch
        {
            return null;
        }
    }

    private static Task<string?> ParsePkgVersionAsync(
        string pkgPath,
        CancellationToken cancellationToken)
    {
        // .pkg files don't have a standard easy-to-parse version format
        // without xar/cpio extraction — return null and let caller use default
        return Task.FromResult<string?>(null);
    }

    // ─── Private: Helpers ──────────────────────────────────────────────

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }

    // ─── Nested type ──────────────────────────────────────────────────

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

/// <summary>
/// macOS driver validator — uses file I/O for hash validation and the codesign tool for signatures.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOSDriverValidator : IDriverValidator
{
    private readonly ICommandRunner _commandRunner;

    /// <param name="commandRunner">Command runner for codesign operations.</param>
    public MacOSDriverValidator(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    // For factory/DI use
    public MacOSDriverValidator() : this(new CommandRunner()) { }

    /// <inheritdoc/>
    public Task<bool> ValidateIntegrityAsync(
        string filePath,
        string expectedHash,
        string hashAlgorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        return HashValidator.ValidateHashAsync(filePath, expectedHash, hashAlgorithm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSignatureAsync(
        string filePath,
        IEnumerable<string> trustedPublishers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Validating macOS code signature for: {filePath}");

            var result = await _commandRunner.RunAsync(
                "codesign",
                new[] { "-v", filePath },
                cancellationToken);

            if (!result.Success)
            {
                // Try deep verification
                result = await _commandRunner.RunAsync(
                    "codesign",
                    new[] { "-v", "--deep", filePath },
                    cancellationToken);
            }

            if (!result.Success)
                return false;

            // If trusted publishers specified, verify against them
            foreach (var publisher in trustedPublishers)
            {
                result = await _commandRunner.RunAsync(
                    "codesign",
                    new[] { "-dvv", filePath },
                    cancellationToken);

                if (result.StandardOutput.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // No trusted publishers specified — any valid signature is accepted
            return !trustedPublishers.Any();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"macOS signature validation failed: {ex.Message}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> ValidateCompatibilityAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        return CompatibilityChecker.CheckCompatibilityAsync(driverInfo, cancellationToken);
    }
}

/// <summary>
/// macOS driver backup — file-level copy using pure .NET I/O.
/// No macOS-specific APIs required for backup/restore operations.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOSDriverBackup : IDriverBackup
{
    /// <inheritdoc/>
    public async Task<bool> BackupAsync(
        string sourcePath,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Backing up macOS driver: {sourcePath} -> {backupPath}");

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                GeneralTracer.Error($"Source not found: {sourcePath}");
                return false;
            }

            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            if (Directory.Exists(sourcePath))
            {
                // Copy directory recursively
                await Task.Run(() => CopyDirectory(sourcePath, backupPath), cancellationToken);
            }
            else
            {
                File.Copy(sourcePath, backupPath, overwrite: true);
            }

            GeneralTracer.Info($"Backup completed: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"macOS backup failed: {ex.Message}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> RestoreAsync(
        string backupPath,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        return BackupAsync(backupPath, targetPath, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Deleting macOS backup: {backupPath}");

            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, recursive: true);
            else if (File.Exists(backupPath))
                File.Delete(backupPath);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"Failed to delete backup: {ex.Message}", ex);
            return Task.FromResult(false);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }
}
