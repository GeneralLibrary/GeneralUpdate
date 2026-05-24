using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Windows crash surveillance strategy using procdump.
/// </summary>
internal sealed class WindowsBowlStrategy : IBowlStrategy
{
    public ProcessStartInfo? Prepare(in BowlContext context)
    {
        GeneralTracer.Info("WindowsBowlStrategy.Prepare: resolving procdump binary.");

        var procdumpExe = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "procdump.exe",
            Architecture.X64 => "procdump64.exe",
            _ => "procdump64a.exe",
        };

        var appDir = Path.Combine(context.TargetPath, "Applications", "Windows");
        var appPath = Path.Combine(appDir, procdumpExe);
        var dumpFullPath = Path.Combine(context.FailDirectory, context.DumpFileName);

        // Map DumpType to procdump flag
        var dumpFlag = context.DumpType switch
        {
            DumpType.Mini => "-mm",
            DumpType.Heap => "-mh",
            _ => "-ma", // Full
        };

        EnsureDirectory(context.FailDirectory);

        var arguments = $"-e {dumpFlag} {context.ProcessNameOrId} \"{dumpFullPath}\"";
        GeneralTracer.Info($"WindowsBowlStrategy.Prepare: app={appPath}, args={arguments}");

        return new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    public Task PostProcessAsync(in BowlContext context,
        ProcessExitResult exitResult, CancellationToken ct)
    {
        // No additional Windows-specific post-processing.
        // Restore, environment variable, and crash report are handled by Bowl itself.
        return Task.CompletedTask;
    }

    private static void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
            StorageManager.DeleteDirectory(path);
        Directory.CreateDirectory(path);
    }
}
