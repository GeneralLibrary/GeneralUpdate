using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Windows system info provider — runs <c>export.bat</c> to collect driver, system, and event log data.
/// </summary>
internal sealed class WindowsSystemInfoProvider : ISystemInfoProvider
{
    public Task ExportAsync(string outputDirectory, CancellationToken ct)
    {
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
            Arguments = outputDirectory,
            UseShellExecute = true,
            CreateNoWindow = true,
        });

        if (process != null)
        {
            GeneralTracer.Info("WindowsSystemInfoProvider: export.bat started.");
            var exited = process.WaitForExit(30_000);
            if (exited)
            {
                GeneralTracer.Info($"WindowsSystemInfoProvider: export.bat finished, exit code {process.ExitCode}.");
            }
            else
            {
                GeneralTracer.Warn("WindowsSystemInfoProvider: export.bat timed out after 30s.");
            }
        }

        return Task.CompletedTask;
    }
}
