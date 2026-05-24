using System.Runtime.InteropServices;
using GeneralUpdate.Core;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Core.Execution;
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
    /// <param name="options">配置选项（可选）/ Configuration options (optional)</param>
    /// <returns>平台特定的驱动更新器实现 / Platform-specific driver updater implementation</returns>
    /// <exception cref="PlatformNotSupportedException">当前平台不支持时抛出 / Thrown when current platform is not supported</exception>
    public static IGeneralDrivelution Create(DrivelutionOptions? options = null)
    {
        // Detect platform and create appropriate implementation
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GeneralTracer.Info("Detected Windows platform, creating WindowsGeneralDrivelution");
            var validator = new WindowsDriverValidator();
            var backup = new WindowsDriverBackup();
            var commandRunner = new CommandRunner();
            return new WindowsGeneralDrivelution(validator, backup, commandRunner, options);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            GeneralTracer.Info("Detected Linux platform, creating LinuxGeneralDrivelution");
            var validator = new LinuxDriverValidator();
            var backup = new LinuxDriverBackup();
            var commandRunner = new CommandRunner();
            return new LinuxGeneralDrivelution(validator, backup, commandRunner, options);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            GeneralTracer.Info("Detected macOS platform, creating MacOsGeneralDrivelution");
            var validator = new MacOSDriverValidator();
            var backup = new MacOSDriverBackup();
            var commandRunner = new CommandRunner();
            return new MacOsGeneralDrivelution(validator, backup, commandRunner, options);
        }
        else
        {
            var osDescription = RuntimeInformation.OSDescription;
            GeneralTracer.Error($"Unsupported platform detected: {osDescription}");
            throw new PlatformNotSupportedException(
                $"Current platform '{osDescription}' is not supported. " +
                "Supported platforms: Windows, Linux, macOS.");
        }
    }

    /// <summary>
    /// 创建适合当前平台的驱动验证器
    /// Creates a driver validator suitable for the current platform
    /// </summary>
    /// <returns>平台特定的驱动验证器实现 / Platform-specific driver validator implementation</returns>
    public static IDriverValidator CreateValidator()
    {
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
            return new MacOSDriverValidator(new CommandRunner());
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
    /// <returns>平台特定的驱动备份实现 / Platform-specific driver backup implementation</returns>
    public static IDriverBackup CreateBackup()
    {
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
            return new MacOSDriverBackup();
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
               RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
               RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }
}
