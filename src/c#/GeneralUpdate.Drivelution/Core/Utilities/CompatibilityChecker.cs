using System.Runtime.InteropServices;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Core.Utilities;

/// <summary>
/// 兼容性检查工具类
/// Compatibility checker utility
/// </summary>
public static class CompatibilityChecker
{
    /// <summary>
    /// 异步检查驱动兼容性
    /// Checks driver compatibility asynchronously
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>是否兼容 / Whether compatible</returns>
    public static Task<bool> CheckCompatibilityAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CheckCompatibility(driverInfo), cancellationToken);
    }

    /// <summary>
    /// 检查驱动兼容性
    /// Checks driver compatibility
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <returns>是否兼容 / Whether compatible</returns>
    public static bool CheckCompatibility(DriverInfo driverInfo)
    {
        if (driverInfo == null)
        {
            throw new ArgumentNullException(nameof(driverInfo));
        }

        // Check OS compatibility
        if (!IsOSCompatible(driverInfo.TargetOS))
        {
            return false;
        }

        // Check architecture compatibility
        if (!IsArchitectureCompatible(driverInfo.Architecture))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 获取当前操作系统名称
    /// Gets current operating system name
    /// </summary>
    public static string GetCurrentOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "MacOS";
        }
        else
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// 获取当前系统架构
    /// Gets current system architecture
    /// </summary>
    public static string GetCurrentArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    /// <summary>
    /// 获取系统版本信息
    /// Gets system version information
    /// </summary>
    public static string GetSystemVersion()
    {
        return Environment.OSVersion.ToString();
    }

    /// <summary>
    /// 检查操作系统是否兼容
    /// Checks if OS is compatible
    /// </summary>
    private static bool IsOSCompatible(string targetOS)
    {
        if (string.IsNullOrWhiteSpace(targetOS))
        {
            // If no target OS specified, assume compatible
            return true;
        }

        var currentOS = GetCurrentOS();
        return targetOS.Contains(currentOS, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查架构是否兼容
    /// Checks if architecture is compatible
    /// </summary>
    private static bool IsArchitectureCompatible(string targetArchitecture)
    {
        if (string.IsNullOrWhiteSpace(targetArchitecture))
        {
            // If no target architecture specified, assume compatible
            return true;
        }

        var currentArch = GetCurrentArchitecture();
        
        // Handle common architecture aliases
        var normalizedTarget = NormalizeArchitecture(targetArchitecture);
        var normalizedCurrent = NormalizeArchitecture(currentArch);

        return normalizedTarget.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 标准化架构名称
    /// Normalizes architecture name
    /// </summary>
    private static string NormalizeArchitecture(string architecture)
    {
        return architecture.ToUpperInvariant() switch
        {
            "X64" or "AMD64" or "X86_64" => "X64",
            "X86" or "I386" or "I686" => "X86",
            "ARM64" or "AARCH64" => "ARM64",
            "ARM" or "ARMV7" => "ARM",
            _ => architecture.ToUpperInvariant()
        };
    }

    /// <summary>
    /// 获取详细的兼容性报告
    /// Gets detailed compatibility report
    /// </summary>
    public static CompatibilityReport GetCompatibilityReport(DriverInfo driverInfo)
    {
        var report = new CompatibilityReport
        {
            CurrentOS = GetCurrentOS(),
            CurrentArchitecture = GetCurrentArchitecture(),
            SystemVersion = GetSystemVersion(),
            TargetOS = driverInfo.TargetOS,
            TargetArchitecture = driverInfo.Architecture,
            OSCompatible = IsOSCompatible(driverInfo.TargetOS),
            ArchitectureCompatible = IsArchitectureCompatible(driverInfo.Architecture)
        };

        report.OverallCompatible = report.OSCompatible && report.ArchitectureCompatible;
        return report;
    }
}

/// <summary>
/// 兼容性报告
/// Compatibility report
/// </summary>
public class CompatibilityReport
{
    public string CurrentOS { get; set; } = string.Empty;
    public string CurrentArchitecture { get; set; } = string.Empty;
    public string SystemVersion { get; set; } = string.Empty;
    public string TargetOS { get; set; } = string.Empty;
    public string TargetArchitecture { get; set; } = string.Empty;
    public bool OSCompatible { get; set; }
    public bool ArchitectureCompatible { get; set; }
    public bool OverallCompatible { get; set; }
}
