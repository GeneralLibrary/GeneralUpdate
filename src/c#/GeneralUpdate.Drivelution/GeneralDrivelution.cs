using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Logging;
using GeneralUpdate.Drivelution.Core.Utilities;
using Serilog;

namespace GeneralUpdate.Drivelution;

/// <summary>
/// 驱动更新器统一入口类 - 提供优雅的API，自动适配平台
/// Unified driver updater entry point - Provides elegant API with automatic platform adaptation
/// </summary>
/// <remarks>
/// 使用示例 / Usage example:
/// <code>
/// // 简单使用 - 自动检测平台
/// // Simple usage - automatic platform detection
/// var updater = GeneralDrivelution.Create();
/// var result = await updater.UpdateAsync(driverInfo, strategy);
/// 
/// // 带配置使用
/// // With configuration
/// var options = new DrivelutionOptions { LogLevel = "Info" };
/// var updater = GeneralDrivelution.Create(options);
/// var result = await updater.UpdateAsync(driverInfo, strategy);
/// </code>
/// </remarks>
public static class GeneralDrivelution
{
    /// <summary>
    /// 创建驱动更新器实例（自动检测当前平台）
    /// Creates a driver updater instance (automatically detects current platform)
    /// </summary>
    /// <param name="options">配置选项（可选）/ Configuration options (optional)</param>
    /// <returns>适配当前平台的驱动更新器 / Platform-adapted driver updater</returns>
    /// <exception cref="PlatformNotSupportedException">当前平台不支持时抛出 / Thrown when platform is not supported</exception>
    public static IGeneralDrivelution Create(DrivelutionOptions? options = null)
    {
        var logger = options != null 
            ? LoggerConfigurator.ConfigureLogger(options) 
            : LoggerConfigurator.CreateDefaultLogger();

        return Core.DrivelutionFactory.Create(logger, options);
    }

    /// <summary>
    /// 创建驱动更新器实例（使用自定义日志记录器）
    /// Creates a driver updater instance (with custom logger)
    /// </summary>
    /// <param name="logger">自定义日志记录器 / Custom logger</param>
    /// <param name="options">配置选项（可选）/ Configuration options (optional)</param>
    /// <returns>适配当前平台的驱动更新器 / Platform-adapted driver updater</returns>
    public static IGeneralDrivelution Create(ILogger logger, DrivelutionOptions? options = null)
    {
        return Core.DrivelutionFactory.Create(logger, options);
    }

    /// <summary>
    /// 快速更新驱动（使用默认配置）
    /// Quick driver update (with default configuration)
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>更新结果 / Update result</returns>
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
    /// 快速更新驱动（带自定义策略）
    /// Quick driver update (with custom strategy)
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="strategy">更新策略 / Update strategy</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>更新结果 / Update result</returns>
    public static async Task<UpdateResult> QuickUpdateAsync(
        DriverInfo driverInfo,
        UpdateStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var updater = Create();
        return await updater.UpdateAsync(driverInfo, strategy, cancellationToken);
    }

    /// <summary>
    /// 验证驱动文件（自动选择平台验证器）
    /// Validates driver file (automatically selects platform validator)
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>是否验证通过 / Whether validation passed</returns>
    public static async Task<bool> ValidateAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        var updater = Create();
        return await updater.ValidateAsync(driverInfo, cancellationToken);
    }

    /// <summary>
    /// 获取当前平台信息
    /// Gets current platform information
    /// </summary>
    /// <returns>平台信息 / Platform information</returns>
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
    /// 从本地目录读取驱动信息
    /// Reads driver information from local directory
    /// </summary>
    /// <param name="directoryPath">目录路径 / Directory path</param>
    /// <param name="searchPattern">搜索模式（可选，例如 "*.inf", "*.ko"）/ Search pattern (optional, e.g., "*.inf", "*.ko")</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>驱动信息列表 / List of driver information</returns>
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
/// 平台信息
/// Platform information
/// </summary>
public class PlatformInfo
{
    /// <summary>平台名称 / Platform name</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>是否支持 / Is supported</summary>
    public bool IsSupported { get; set; }

    /// <summary>操作系统 / Operating system</summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>系统架构 / Architecture</summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>系统版本 / System version</summary>
    public string SystemVersion { get; set; } = string.Empty;

    /// <summary>
    /// 返回平台信息的字符串表示
    /// Returns string representation of platform information
    /// </summary>
    public override string ToString()
    {
        return $"{Platform} ({OperatingSystem}) - {Architecture} - {SystemVersion} - " +
               $"Supported: {(IsSupported ? "Yes" : "No")}";
    }
}
