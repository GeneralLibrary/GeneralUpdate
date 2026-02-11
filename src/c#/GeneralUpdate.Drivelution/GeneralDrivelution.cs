using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Events;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Logging;
using GeneralUpdate.Drivelution.Core.Utilities;

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
        var logger = LoggerConfigurator.ConfigureLogger(options);
        return Core.DrivelutionFactory.Create(logger, options);
    }

    /// <summary>
    /// Creates a driver updater instance (with custom logger)
    /// </summary>
    /// <param name="logger">Custom logger</param>
    /// <param name="options">Configuration options (optional)</param>
    /// <returns>Platform-adapted driver updater</returns>
    public static IGeneralDrivelution Create(IDrivelutionLogger logger, DrivelutionOptions? options = null)
    {
        return Core.DrivelutionFactory.Create(logger, options);
    }

    /// <summary>
    /// Quick driver update (with default configuration)
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update result</returns>
    public static async Task<UpdateResult> QuickUpdateAsync(
        DriverInfo driverInfo, 
        CancellationToken cancellationToken = default)
    {
        var updater = Create();
        var strategy = new UpdateStrategy
        {
            RequireBackup = true,
            RetryCount = 3,
            RetryIntervalSeconds = 5
        };
        
        return await updater.UpdateAsync(driverInfo, strategy, cancellationToken);
    }

    /// <summary>
    /// Quick driver update (with custom strategy)
    /// </summary>
    /// <param name="driverInfo">Driver information</param>
    /// <param name="strategy">Update strategy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update result</returns>
    public static async Task<UpdateResult> QuickUpdateAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var updater = Create();
        return await updater.UpdateAsync(driverInfo, strategy, cancellationToken);
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
        var updater = Create();
        return await updater.ValidateAsync(driverInfo, cancellationToken);
    }

    /// <summary>
    /// Gets current platform information
    /// </summary>
    /// <returns>Platform information</returns>
    public static PlatformInfo GetPlatformInfo()
    {
        return new PlatformInfo
        {
            Platform = Core.DrivelutionFactory.GetCurrentPlatform(),
            IsSupported = Core.DrivelutionFactory.IsPlatformSupported(),
            OperatingSystem = CompatibilityChecker.GetCurrentOS(),
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            SystemVersion = CompatibilityChecker.GetSystemVersion()
        };
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
        var updater = Create();
        return await updater.GetDriversFromDirectoryAsync(directoryPath, searchPattern, cancellationToken);
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
