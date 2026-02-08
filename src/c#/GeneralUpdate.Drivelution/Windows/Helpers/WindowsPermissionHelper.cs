using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.Windows.Helpers;

/// <summary>
/// Windows权限助手
/// Windows permission helper
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsPermissionHelper
{
    /// <summary>
    /// 检查当前进程是否具有管理员权限
    /// Checks if current process has administrator privileges
    /// </summary>
    /// <returns>是否具有管理员权限 / Whether has administrator privileges</returns>
    public static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // Failed to check administrator privileges - return false
            return false;
        }
    }

    /// <summary>
    /// 尝试提升权限（触发UAC）
    /// Attempts to elevate privileges (triggers UAC)
    /// </summary>
    /// <param name="executablePath">可执行文件路径 / Executable path</param>
    /// <param name="arguments">参数 / Arguments</param>
    /// <param name="silent">是否静默提权 / Whether to elevate silently</param>
    /// <returns>是否成功 / Whether successful</returns>
    public static bool TryElevatePrivileges(string executablePath, string arguments = "", bool silent = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("This method is only supported on Windows");
        }

        if (IsAdministrator())
        {
            // Already running with administrator privileges
            return true;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = executablePath,
                Arguments = arguments,
                Verb = "runas" // This triggers UAC elevation
            };

            if (silent)
            {
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt
            return false;
        }
        catch (Exception)
        {
            // Failed to elevate privileges
            return false;
        }
    }

    /// <summary>
    /// 确保管理员权限，如无则抛出异常
    /// Ensures administrator privileges, throws exception if not
    /// </summary>
    public static void EnsureAdministrator()
    {
        if (!IsAdministrator())
        {
            throw new DriverPermissionException(
                "Administrator privileges are required to perform driver updates. " +
                "Please run this application as administrator.");
        }
    }

    /// <summary>
    /// 重启当前进程以获取管理员权限
    /// Restarts current process to obtain administrator privileges
    /// </summary>
    /// <returns>是否成功 / Whether successful</returns>
    public static bool RestartAsAdministrator()
    {
        if (IsAdministrator())
        {
            return true;
        }

        var currentProcess = Process.GetCurrentProcess();
        var exePath = currentProcess.MainModule?.FileName ?? string.Empty;
        
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("Cannot determine current executable path");
        }

        var args = Environment.GetCommandLineArgs().Skip(1);
        var argsString = string.Join(" ", args);

        if (TryElevatePrivileges(exePath, argsString))
        {
            Environment.Exit(0);
            return true;
        }

        return false;
    }
}
