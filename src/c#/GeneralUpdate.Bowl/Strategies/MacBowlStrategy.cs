using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// macOS crash surveillance strategy.
/// Uses the built-in <c>lldb</c> debugger for crash capture (procdump does not support macOS).
/// </summary>
/// <remarks>
/// Note: <c>lldb</c> requires the process being debugged to allow debugging
/// (SIP / task_for_pid restrictions). For production macOS crash capture, consider
/// using Crashpad (https://chromium.googlesource.com/crashpad/crashpad/) instead.
/// This implementation provides a basic stub that can be extended.
/// </remarks>
internal sealed class MacBowlStrategy : IBowlStrategy
{
    public ProcessStartInfo? Prepare(in BowlContext context)
    {
        if (!IsLldbAvailable())
        {
            GeneralTracer.Warn(
                "MacBowlStrategy.Prepare: lldb not available. macOS crash monitoring is unavailable.");
            return null;
        }

        var dumpFullPath = Path.Combine(context.FailDirectory, context.DumpFileName);
        EnsureDirectory(context.FailDirectory);

        // lldb batch mode: attach to process by name, save core dump, quit.
        // Use separate -o arguments per command to avoid nested quoting issues.
        GeneralTracer.Info($"MacBowlStrategy.Prepare: target={context.ProcessNameOrId}");

        return new ProcessStartInfo
        {
            FileName = "/usr/bin/lldb",
            Arguments = string.Concat(
                "--batch",
                $" -o \"process attach --name {context.ProcessNameOrId} --waitfor\"",
                $" -o \"process save-core {dumpFullPath}\"",
                " -o quit"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    public Task PostProcessAsync(in BowlContext context,
        ProcessExitResult exitResult, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private static bool IsLldbAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/which",
                    Arguments = "lldb",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            var exited = process.WaitForExit(3000);
            if (!exited)
            {
                process.Kill();
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }
}
