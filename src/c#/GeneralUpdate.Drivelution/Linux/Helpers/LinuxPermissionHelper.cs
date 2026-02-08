using System.Diagnostics;
using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.Linux.Helpers;

/// <summary>
/// Linux权限助手
/// Linux permission helper
/// </summary>
[SupportedOSPlatform("linux")]
public static class LinuxPermissionHelper
{
    /// <summary>
    /// 检查当前用户是否具有root权限
    /// Checks if current user has root privileges
    /// </summary>
    /// <returns>是否具有root权限 / Whether has root privileges</returns>
    public static bool IsRoot()
    {
        try
        {
            var userId = Environment.GetEnvironmentVariable("UID") ?? string.Empty;
            if (userId == "0")
            {
                return true;
            }

            // Alternative check using 'id' command
            var startInfo = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output == "0";
            }

            return false;
        }
        catch (Exception)
        {
            // Failed to check root privileges
            return false;
        }
    }

    /// <summary>
    /// 检查当前用户是否可以使用sudo
    /// Checks if current user can use sudo
    /// </summary>
    /// <returns>是否可以使用sudo / Whether can use sudo</returns>
    public static async Task<bool> CanUseSudoAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = "-n true",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception)
        {
            // Failed to check sudo privileges
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
                "Root privileges are required to perform driver updates. " +
                "Please run this application with sudo or as root user.");
        }
    }

    /// <summary>
    /// 确保sudo权限，如无则抛出异常
    /// Ensures sudo privileges, throws exception if not
    /// </summary>
    public static async Task EnsureSudoAsync()
    {
        if (IsRoot())
        {
            return;
        }

        if (!await CanUseSudoAsync())
        {
            throw new DriverPermissionException(
                "Sudo privileges are required to perform driver updates. " +
                "Please ensure your user has sudo access.");
        }
    }

    /// <summary>
    /// 使用sudo执行命令
    /// Executes command with sudo
    /// </summary>
    /// <param name="command">命令 / Command</param>
    /// <param name="arguments">参数 / Arguments</param>
    /// <returns>是否成功 / Whether successful</returns>
    public static async Task<(bool success, string output, string error)> ExecuteWithSudoAsync(
        string command,
        string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"{command} {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return (process.ExitCode == 0, output, error);
            }

            return (false, string.Empty, "Failed to start process");
        }
        catch (Exception ex)
        {
            // Failed to execute command with sudo
            return (false, string.Empty, ex.Message);
        }
    }
}
