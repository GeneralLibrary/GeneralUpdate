using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Windows system info provider — runs <c>export.bat</c> to collect driver, system, and event log data.
/// </summary>
internal sealed class WindowsSystemInfoProvider : ISystemInfoProvider
{
    private const int ExportTimeoutMs = 30_000;

    public Task ExportAsync(string outputDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        GeneralTracer.Info($"WindowsSystemInfoProvider.ExportAsync: exporting to {outputDirectory}.");

        var appDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Applications", "Windows");
        var batPath = Path.Combine(appDir, "export.bat");

        if (!File.Exists(batPath))
        {
            GeneralTracer.Error($"WindowsSystemInfoProvider: export.bat not found at {batPath}.");
            throw new FileNotFoundException("export.bat not found!", batPath);
        }

        // export.bat takes the output directory as first argument
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            Arguments = $"\"{outputDirectory}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Failed to start export.bat.");

        GeneralTracer.Info("WindowsSystemInfoProvider: export.bat started.");
        var stopwatch = Stopwatch.StartNew();
        while (!process.WaitForExit(250))
        {
            if (ct.IsCancellationRequested)
            {
                TerminateProcessTree(process);
                ct.ThrowIfCancellationRequested();
            }

            if (stopwatch.ElapsedMilliseconds >= ExportTimeoutMs)
            {
                GeneralTracer.Warn($"WindowsSystemInfoProvider: export.bat timed out after {ExportTimeoutMs}ms; terminating its process tree.");
                TerminateProcessTree(process);
                break;
            }
        }

        if (process.HasExited)
            GeneralTracer.Info($"WindowsSystemInfoProvider: export.bat finished, exit code {process.ExitCode}.");
        return Task.CompletedTask;
    }

    private static void TerminateProcessTree(Process process)
    {
        if (process.HasExited)
            return;

        var taskkillPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
        using var taskkill = Process.Start(new ProcessStartInfo
        {
            FileName = taskkillPath,
            Arguments = $"/PID {process.Id} /T /F",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (taskkill == null)
            throw new InvalidOperationException("Failed to start taskkill for the timed-out export process.");

        if (!taskkill.WaitForExit(10_000))
        {
            try
            {
                taskkill.Kill();
                taskkill.WaitForExit();
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("WindowsSystemInfoProvider: failed to stop taskkill.", ex);
            }
            throw new TimeoutException("Timed out while terminating the system-info export process tree.");
        }

        if (!process.WaitForExit(5_000))
            throw new TimeoutException($"System-info export process {process.Id} remained active after taskkill.");
    }
}
