using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.MacOS.Helpers;

/// <summary>
/// macOS权限助手
/// macOS permission helper
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacOSPermissionHelper
{
    [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static extern uint _geteuid();

    /// <summary>
    /// 检查当前用户是否具有root权限
    /// Checks if current user has root privileges (euid == 0)
    /// </summary>
    /// <returns>是否具有root权限 / Whether has root privileges</returns>
    public static bool IsRoot()
    {
        try
        {
            return _geteuid() == 0;
        }
        catch
        {
            // Failed to check root privileges via P/Invoke
            return false;
        }
    }

    /// <summary>
    /// 确保root权限，如无则抛出异常
    /// Ensures root privileges, throws exception if not
    /// </summary>
    public static void EnsureRoot()
    {
        if (!IsRoot())
        {
            throw new DriverPermissionException(
                "Root privileges are required to perform driver updates on macOS. " +
                "Please run this application with sudo or as root user.");
        }
    }
}
