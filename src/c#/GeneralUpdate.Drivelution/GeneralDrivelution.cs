using GeneralUpdate.Core;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralUpdate.Drivelution;

/// <summary>
/// Unified driver updater entry point - Provides elegant API with automatic platform adaptation
/// </summary>
/// <remarks>
/// Usage example:
/// <code>
/// // Simple usage - automatic platform detection
/// var updater = GeneralDrivelution.Create();
/// var result = await updater.UpdateAsync(driverInfo, strategy);
/// 
/// // With configuration
/// var options = new DrivelutionOptions { };
/// var updater = GeneralDrivelution.Create(options);
/// var result = await updater.UpdateAsync(driverInfo, strategy);
/// </code>
/// </remarks>
public static class GeneralDrivelution
{
    /// <summary>
    /// Creates a driver updater instance (automatically detects current platform)
    /// </summary>
    /// <param name="options">Configuration options (optional)</param>
    /// <returns>Platform-adapted driver updater</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when platform is not supported</exception>
    public static IGeneralDrivelution Create(DrivelutionOptions? options = null)
    {
        return Core.DrivelutionFactory.Create(options);
    }

    /// <summary>
    /// Resolves a driver updater from the DI service provider.
    /// Falls back to <see cref="Create(DrivelutionOptions?)"/> if not registered.
    /// </summary>
    /// <param name="serviceProvider">DI service provider.</param>
    /// <returns>Platform-adapted driver updater.</returns>
    public static IGeneralDrivelution Create(IServiceProvider serviceProvider)
    {
        var updater = serviceProvider.GetService<IGeneralDrivelution>();
        if (updater is not null)
            return updater;

        var options = serviceProvider.GetService<DrivelutionOptions>();
        return Core.DrivelutionFactory.Create(options);
    }

    /// <summary>
    /// Quick driver update (with optional custom strategy; falls back to safe defaults)
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="strategy">Update strategy (optional, defaults to backup+retry)</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update result</returns>
    public static async Task<UpdateResult> QuickUpdateAsync(
        DriverInfo driverInfo,
        UpdateStrategy? strategy = null,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        strategy ??= new UpdateStrategy
        {
            RequireBackup = true,
            RetryCount = 3,
            RetryIntervalSeconds = 5
        };

        GeneralTracer.Info($"GeneralDrivelution.QuickUpdateAsync: starting driver update. Driver={driverInfo.Name}, Version={driverInfo.Version}");
        var updater = Create();
        var result = await updater.UpdateAsync(driverInfo, strategy, progress, cancellationToken);
        GeneralTracer.Info($"GeneralDrivelution.QuickUpdateAsync: driver update completed. Success={result.Success}, Status={result.Status}, DurationMs={result.DurationMs}");
        return result;
    }

    /// <summary>
    /// Validates driver file (automatically selects platform validator)
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether validation passed</returns>
    public static async Task<bool> ValidateAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"GeneralDrivelution.ValidateAsync: validating driver file. Driver={driverInfo.Name}, FilePath={driverInfo.FilePath}");
        var updater = Create();
        var result = await updater.ValidateAsync(driverInfo, cancellationToken);
        GeneralTracer.Info($"GeneralDrivelution.ValidateAsync: driver validation completed. Driver={driverInfo.Name}, IsValid={result}");
        return result;
    }

    /// <summary>
    /// Gets current platform information
    /// </summary>
    /// <returns>Platform information</returns>
    public static PlatformInfo GetPlatformInfo()
    {
        GeneralTracer.Info("GeneralDrivelution.GetPlatformInfo: retrieving platform information.");
        var info = new PlatformInfo
        {
            Platform = Core.DrivelutionFactory.GetCurrentPlatform(),
            IsSupported = Core.DrivelutionFactory.IsPlatformSupported(),
            OperatingSystem = CompatibilityChecker.GetCurrentOS(),
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            SystemVersion = CompatibilityChecker.GetSystemVersion()
        };
        GeneralTracer.Info($"GeneralDrivelution.GetPlatformInfo: Platform={info.Platform}, OS={info.OperatingSystem}, Arch={info.Architecture}, Version={info.SystemVersion}, Supported={info.IsSupported}");
        return info;
    }

    /// <summary>
    /// Reads driver information from local directory
    /// </summary>
    /// <param name="directoryPath">Directory path</param>
    /// <param name="searchPattern">Search pattern (optional, e.g., "*.inf", "*.ko")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of driver information</returns>
    public static async Task<List<DriverInfo>> GetDriversFromDirectoryAsync(
        string directoryPath,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"GeneralDrivelution.GetDriversFromDirectoryAsync: scanning directory for drivers. Path={directoryPath}, SearchPattern={searchPattern ?? "(default)"}");
        var updater = Create();
        var drivers = await updater.GetDriversFromDirectoryAsync(directoryPath, searchPattern, cancellationToken);
        GeneralTracer.Info($"GeneralDrivelution.GetDriversFromDirectoryAsync: found {drivers?.Count ?? 0} driver(s) in directory={directoryPath}");
        return drivers;
    }

    /// <summary>
    /// Batch updates multiple drivers
    /// </summary>
    /// <param name="drivers">Drivers to update</param>
    /// <param name="strategy">Update strategy</param>
    /// <param name="mode">Execution mode (Sequential or Parallel)</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated batch result</returns>
    public static async Task<BatchUpdateResult> BatchUpdateAsync(
        IEnumerable<DriverInfo> drivers,
        UpdateStrategy strategy,
        BatchMode mode = BatchMode.Sequential,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info("GeneralDrivelution.BatchUpdateAsync: starting batch driver update");
        var updater = Create();
        var result = await updater.BatchUpdateAsync(drivers, strategy, mode, progress, cancellationToken);
        GeneralTracer.Info($"GeneralDrivelution.BatchUpdateAsync: batch complete. OK={result.SucceededCount}, Failed={result.FailedCount}");
        return result;
    }
}

/// <summary>
/// Platform information
/// </summary>
public class PlatformInfo
{
    /// <summary>Platform name</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Is supported</summary>
    public bool IsSupported { get; set; }

    /// <summary>Operating system</summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>Architecture</summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>System version</summary>
    public string SystemVersion { get; set; } = string.Empty;

    /// <summary>
    /// Returns string representation of platform information
    /// </summary>
    public override string ToString()
    {
        return $"{Platform} ({OperatingSystem}) - {Architecture} - {SystemVersion} - " +
               $"Supported: {(IsSupported ? "Yes" : "No")}";
    }
}
