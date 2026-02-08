using System.Diagnostics;
using System.Runtime.InteropServices;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Core.Utilities;

/// <summary>
/// 重启助手工具类
/// Restart helper utility
/// </summary>
public static class RestartHelper
{
    /// <summary>
    /// 根据重启模式执行重启操作
    /// Executes restart operation based on restart mode
    /// </summary>
    /// <param name="mode">重启模式 / Restart mode</param>
    /// <param name="delaySeconds">延迟秒数（仅用于延迟重启）/ Delay seconds (only for delayed restart)</param>
    /// <param name="message">提示消息 / Prompt message</param>
    /// <returns>是否成功 / Whether successful</returns>
    public static async Task<bool> HandleRestartAsync(RestartMode mode, int delaySeconds = 30, string message = "")
    {
        switch (mode)
        {
            case RestartMode.None:
                return true;

            case RestartMode.Prompt:
                return PromptUserForRestart(message);

            case RestartMode.Delayed:
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                return await RestartSystemAsync();

            case RestartMode.Immediate:
                return await RestartSystemAsync();

            default:
                return false;
        }
    }

    /// <summary>
    /// 提示用户重启
    /// Prompts user for restart
    /// </summary>
    /// <param name="message">提示消息 / Prompt message</param>
    /// <returns>用户是否选择重启 / Whether user chose to restart</returns>
    public static bool PromptUserForRestart(string message = "")
    {
        var promptMessage = string.IsNullOrWhiteSpace(message)
            ? "Driver update requires system restart. Do you want to restart now? (Y/N)"
            : message;

        Console.WriteLine(promptMessage);
        // This is a simplified implementation. In a real application, you might use a GUI dialog.
        // For now, we'll just log the message and return false (user needs to manually restart)
        return false;
    }

    /// <summary>
    /// 异步重启系统
    /// Restarts system asynchronously
    /// </summary>
    /// <returns>是否成功启动重启命令 / Whether restart command was successfully initiated</returns>
    public static async Task<bool> RestartSystemAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await RestartWindowsAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await RestartLinuxAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await RestartMacOSAsync();
            }
            else
            {
                throw new PlatformNotSupportedException("Current platform is not supported for restart");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restart system: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 重启Windows系统
    /// Restarts Windows system
    /// </summary>
    private static async Task<bool> RestartWindowsAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/r /t 0",
            UseShellExecute = false,
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

    /// <summary>
    /// 重启Linux系统
    /// Restarts Linux system
    /// </summary>
    private static async Task<bool> RestartLinuxAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "-r now",
            UseShellExecute = false,
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

    /// <summary>
    /// 重启MacOS系统
    /// Restarts MacOS system
    /// </summary>
    private static async Task<bool> RestartMacOSAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "-r now",
            UseShellExecute = false,
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

    /// <summary>
    /// 重启当前进程
    /// Restarts current process
    /// </summary>
    public static void RestartCurrentProcess()
    {
        var currentProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = currentProcess.MainModule?.FileName ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }

    /// <summary>
    /// 检查是否需要重启
    /// Checks if restart is required
    /// </summary>
    /// <param name="mode">重启模式 / Restart mode</param>
    /// <returns>是否需要重启 / Whether restart is required</returns>
    public static bool IsRestartRequired(RestartMode mode)
    {
        return mode != RestartMode.None;
    }
}
