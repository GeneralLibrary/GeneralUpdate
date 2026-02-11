using System.Runtime.InteropServices;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Events;
using GeneralUpdate.Drivelution.Windows.Implementation;
using GeneralUpdate.Drivelution.Linux.Implementation;
using GeneralUpdate.Drivelution.MacOS.Implementation;

namespace GeneralUpdate.Drivelution.Core;

/// <summary>
/// 驱动更新器工厂类 - 自动检测平台并创建相应的实现
/// Driver updater factory - Automatically detects platform and creates appropriate implementation
/// </summary>
public static class DrivelutionFactory
{
    /// <summary>
    /// 创建适合当前平台的驱动更新器
    /// Creates a driver updater suitable for the current platform
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="options">配置选项（可选）/ Configuration options (optional)</param>
    /// <returns>平台特定的驱动更新器实现 / Platform-specific driver updater implementation</returns>
    /// <exception cref="PlatformNotSupportedException">当前平台不支持时抛出 / Thrown when current platform is not supported</exception>
    public static IGeneralDrivelution Create(IDrivelutionLogger? logger = null, DrivelutionOptions? options = null)
    {
        // Note: Logger parameter is kept for backward compatibility with existing callers,
        // but internally the implementation now uses GeneralTracer for logging
        logger ??= CreateDefaultLogger(options);

        // Detect platform and create appropriate implementation
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logger.Information("Detected Windows platform, creating WindowsGeneralDrivelution");
            var validator = new WindowsDriverValidator();
            var backup = new WindowsDriverBackup();
            return new WindowsGeneralDrivelution(validator, backup);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.Information("Detected Linux platform, creating LinuxGeneralDrivelution");
            var validator = new LinuxDriverValidator();
            var backup = new LinuxDriverBackup();
            return new LinuxGeneralDrivelution(validator, backup);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            logger.Warning("MacOS platform detected but not yet implemented");
            throw new PlatformNotSupportedException(
                "MacOS driver update is not yet implemented. " +
                "This is a placeholder for future MacOS support. " +
                "Current platform: macOS");
        }
        else
        {
            var osDescription = RuntimeInformation.OSDescription;
            logger.Error($"Unsupported platform detected: {osDescription}");
            throw new PlatformNotSupportedException(
                $"Current platform '{osDescription}' is not supported. " +
                "Supported platforms: Windows (8+), Linux (Ubuntu 18.04+, CentOS 7+, Debian 10+)");
        }
    }

    /// <summary>
    /// 创建适合当前平台的驱动验证器
    /// Creates a driver validator suitable for the current platform
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <returns>平台特定的驱动验证器实现 / Platform-specific driver validator implementation</returns>
    public static IDriverValidator CreateValidator(IDrivelutionLogger? logger = null)
    {
        logger ??= CreateDefaultLogger();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsDriverValidator();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxDriverValidator();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("MacOS driver validator is not yet implemented");
        }
        else
        {
            throw new PlatformNotSupportedException($"Platform not supported: {RuntimeInformation.OSDescription}");
        }
    }

    /// <summary>
    /// 创建适合当前平台的驱动备份管理器
    /// Creates a driver backup manager suitable for the current platform
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <returns>平台特定的驱动备份实现 / Platform-specific driver backup implementation</returns>
    public static IDriverBackup CreateBackup(IDrivelutionLogger? logger = null)
    {
        logger ??= CreateDefaultLogger();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsDriverBackup();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxDriverBackup();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("MacOS driver backup is not yet implemented");
        }
        else
        {
            throw new PlatformNotSupportedException($"Platform not supported: {RuntimeInformation.OSDescription}");
        }
    }

    /// <summary>
    /// 获取当前平台名称
    /// Gets the current platform name
    /// </summary>
    /// <returns>平台名称 / Platform name</returns>
    public static string GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "MacOS";
        else
            return "Unknown";
    }

    /// <summary>
    /// 检查当前平台是否支持
    /// Checks if the current platform is supported
    /// </summary>
    /// <returns>是否支持 / Whether supported</returns>
    public static bool IsPlatformSupported()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
               RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        // MacOS is planned but not yet implemented
    }

    /// <summary>
    /// 创建默认日志记录器
    /// Creates a default logger
    /// </summary>
    private static IDrivelutionLogger CreateDefaultLogger(DrivelutionOptions? options = null)
    {
        return Logging.LoggerConfigurator.ConfigureLogger(options);
    }
}
