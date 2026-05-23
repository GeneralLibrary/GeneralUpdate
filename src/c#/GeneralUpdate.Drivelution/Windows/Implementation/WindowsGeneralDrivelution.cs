using System.Runtime.Versioning;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Execution;
using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Windows.Helpers;

namespace GeneralUpdate.Drivelution.Windows.Implementation;

/// <summary>
/// Windows driver updater implementation.
/// Inherits the unified pipeline from <see cref="BaseDriverUpdater"/> and adds Windows-specific
/// permission checks, PnPUtil-based installation, and INF file parsing.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsGeneralDrivelution : BaseDriverUpdater
{
    private readonly ICommandRunner _commandRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsGeneralDrivelution"/> class.
    /// </summary>
    /// <param name="validator">Driver validator.</param>
    /// <param name="backup">Driver backup manager.</param>
    /// <param name="commandRunner">Command runner for PnPUtil operations.</param>
    /// <param name="options">Configuration options (optional).</param>
    public WindowsGeneralDrivelution(
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
        // Prepend Windows permission check before the default pipeline
        yield return CreatePermissionCheckStep();

        foreach (var step in base.GetPipelineSteps(strategy))
            yield return step;
    }

    /// <inheritdoc/>
    protected override async Task InstallCoreAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"Installing Windows driver via PnPUtil: {driverInfo.FilePath}");
        await InstallDriverUsingPnPUtilAsync(driverInfo.FilePath, cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task<bool> VerifyInstallationAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            GeneralTracer.Info($"Verifying Windows driver installation for: {driverInfo.FilePath}");

            var result = await _commandRunner.RunAsync(
                "pnputil.exe",
                new[] { "/enum-drivers" },
                cancellationToken);

            var driverFileName = Path.GetFileName(driverInfo.FilePath);
            var driverName = Path.GetFileNameWithoutExtension(driverInfo.FilePath);

            bool isInstalled = result.StandardOutput.Contains(driverFileName, StringComparison.OrdinalIgnoreCase)
                            || result.StandardOutput.Contains(driverName, StringComparison.OrdinalIgnoreCase);

            GeneralTracer.Info($"Driver verification result: {isInstalled}");
            return isInstalled;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Failed to verify driver installation - {ex.Message}");
            return true; // Non-fatal: don't block the update
        }
    }

    // ─── Rollback override ─────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task<bool> RollbackAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GeneralTracer.Info($"Rolling back Windows driver from backup: {backupPath}");

            if (!Directory.Exists(backupPath))
            {
                GeneralTracer.Error($"Backup directory not found: {backupPath}");
                return false;
            }

            var backupFiles = Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories);
            if (backupFiles.Length == 0)
            {
                GeneralTracer.Warn($"No backup files found in: {backupPath}");
                return false;
            }

            GeneralTracer.Info($"Found {backupFiles.Length} backup files");

            // Reinstall backed-up INF drivers via PnPUtil
            foreach (var infFile in backupFiles.Where(
                f => f.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    GeneralTracer.Info($"Restoring driver from: {infFile}");
                    await InstallDriverUsingPnPUtilAsync(infFile, cancellationToken);
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to restore driver from {infFile}: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Windows driver rollback failed", ex);
            throw new DriverRollbackException($"Failed to rollback driver: {ex.Message}", ex);
        }
    }

    // ─── Driver discovery overrides ────────────────────────────────────

    /// <inheritdoc/>
    protected override string GetDefaultSearchPattern() => "*.inf";

    /// <inheritdoc/>
    public override async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        var drivers = new List<DriverInfo>();

        try
        {
            GeneralTracer.Info($"Reading Windows drivers from directory: {directoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                GeneralTracer.Warn($"Directory not found: {directoryPath}");
                return drivers;
            }

            var pattern = searchPattern ?? "*.inf";
            var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var info = await ParseWindowsDriverFileAsync(filePath, cancellationToken);
                    if (info is not null)
                        drivers.Add(info);
                }
                catch (Exception ex)
                {
                    GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
                }
            }

            GeneralTracer.Info($"Loaded {drivers.Count} Windows driver(s) from directory");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"Error reading drivers from directory: {directoryPath}", ex);
        }

        return drivers;
    }

    // ─── Private helpers ────────────────────────────────────────────────

    /// <summary>
    /// Creates a Windows admin-privilege check as the first pipeline step.
    /// </summary>
    private static IPipelineStep CreatePermissionCheckStep()
    {
        return new DelegateStep("CheckPermissions",
            execute: (context, ct) =>
            {
                context.Result.StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] Checking Windows administrator privileges");

                if (!WindowsPermissionHelper.IsAdministrator())
                {
                    return Task.FromResult(PipelineResult.Fail(
                        "Administrator privileges are required for driver updates. " +
                        "Please restart the application as administrator."));
                }

                return Task.FromResult(PipelineResult.Ok());
            });
    }

    /// <summary>
    /// Installs a driver using the Windows PnPUtil command-line tool (via ICommandRunner).
    /// </summary>
    private async Task InstallDriverUsingPnPUtilAsync(
        string driverPath,
        CancellationToken cancellationToken)
    {
        GeneralTracer.Info($"PnPUtil installing: {driverPath}");

        var result = await _commandRunner.RunAsync(
            "pnputil.exe",
            new[] { "/add-driver", driverPath, "/install" },
            cancellationToken);

        GeneralTracer.Info($"PnPUtil output: {result.StandardOutput.Trim()}");

        if (!result.Success)
        {
            GeneralTracer.Error($"PnPUtil failed (exit {result.ExitCode}): {result.StandardError}");
            throw new DriverInstallationException(
                $"PnPUtil failed with exit code {result.ExitCode}: {result.StandardError}");
        }
    }

    /// <summary>
    /// Parses a Windows driver file (INF) and extracts metadata, hash, and signature info.
    /// </summary>
    private async Task<DriverInfo?> ParseWindowsDriverFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverInfo = new DriverInfo
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                TargetOS = "Windows",
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };

            // Parse INF metadata
            if (filePath.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                await ParseInfFileAsync(filePath, driverInfo, cancellationToken);
            }

            // Compute file hash
            driverInfo.Hash = await HashValidator.ComputeHashAsync(filePath, "SHA256", cancellationToken);
            driverInfo.HashAlgorithm = "SHA256";

            // Extract signature info
            ExtractSignatureInfo(filePath, driverInfo);

            return driverInfo;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Failed to parse driver file: {filePath} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts digital signature / publisher information from a signed Windows driver file.
    /// </summary>
    private static void ExtractSignatureInfo(string filePath, DriverInfo driverInfo)
    {
        if (!WindowsSignatureHelper.IsFileSigned(filePath))
            return;

        try
        {
            using var cert2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(filePath);
            var subject = cert2.Subject;
            var cnIndex = subject.IndexOf("CN=", StringComparison.Ordinal);

            if (cnIndex < 0)
                return;

            var cnStart = cnIndex + 3;
            var cnEnd = subject.IndexOf(',', cnStart);

            var publisher = cnEnd > cnStart
                ? subject[cnStart..cnEnd]
                : subject[cnStart..];

            if (!string.IsNullOrEmpty(publisher))
                driverInfo.TrustedPublishers.Add(publisher);
        }
        catch (Exception ex)
        {
            GeneralTracer.Debug($"Could not extract signature for {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a Windows INF file to extract DriverVer, DriverDesc, and HardwareId.
    /// </summary>
    private static async Task ParseInfFileAsync(
        string infPath,
        DriverInfo driverInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(infPath, cancellationToken);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("DriverVer", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length > 1)
                    {
                        var verParts = parts[1].Split(',');
                        if (verParts.Length > 1)
                            driverInfo.Version = verParts[1].Trim();
                        if (verParts.Length > 0
                            && DateTime.TryParse(verParts[0].Trim(), out var releaseDate))
                            driverInfo.ReleaseDate = releaseDate;
                    }
                }
                else if (trimmed.StartsWith("DriverDesc", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length > 1)
                        driverInfo.Description = parts[1].Trim().Trim('"', '%');
                }
                else if (trimmed.StartsWith("HardwareId", StringComparison.OrdinalIgnoreCase)
                      || trimmed.Contains("HW_ID", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length > 1)
                        driverInfo.HardwareId = parts[1].Trim().Trim('"');
                }
            }

            if (string.IsNullOrEmpty(driverInfo.Version))
                driverInfo.Version = "1.0.0";
        }
        catch (Exception ex)
        {
            GeneralTracer.Debug($"Could not parse INF file: {infPath} - {ex.Message}");
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
