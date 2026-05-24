using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Core;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Linux crash surveillance strategy using procdump.
/// Fixes the dead-code issue in the legacy <c>LinuxStrategy</c> — the strategy factory
/// now correctly creates this strategy on Linux platforms.
/// </summary>
internal sealed class LinuxBowlStrategy : IBowlStrategy
{
    private static readonly Dictionary<string, string> DistroPackageMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["ubuntu"]  = "procdump_3.3.0_amd64.deb",
        ["debian"]  = "procdump_3.3.0_amd64.deb",
        ["rhel"]    = "procdump-3.3.0-0.el8.x86_64.rpm",
        ["centos"]  = "procdump-3.3.0-0.el8.x86_64.rpm",
        ["fedora"]  = "procdump-3.3.0-0.el8.x86_64.rpm",
        ["clearos"] = "procdump-3.3.0-0.cm2.x86_64.rpm",
    };

    private bool _procdumpInstalled;

    public ProcessStartInfo? Prepare(in BowlContext context)
    {
        // Lazy install procdump if not already done
        if (!_procdumpInstalled)
        {
            var installed = TryInstallProcdump();
            if (!installed)
            {
                GeneralTracer.Warn(
                    "LinuxBowlStrategy.Prepare: procdump installation failed on this system.");
                _procdumpInstalled = false;
                // Don't throw; return null signals "tool unavailable" to Bowl (graceful degradation)
            }
            else
            {
                _procdumpInstalled = true;
            }
        }

        if (!_procdumpInstalled)
            return null;

        var dumpFullPath = Path.Combine(context.FailDirectory, context.DumpFileName);
        EnsureDirectory(context.FailDirectory);

        GeneralTracer.Info($"LinuxBowlStrategy.Prepare: target={context.ProcessNameOrId}, dump={dumpFullPath}");

        return new ProcessStartInfo
        {
            FileName = "procdump",
            Arguments = $"-p {context.ProcessNameOrId} -o \"{dumpFullPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    public Task PostProcessAsync(in BowlContext context,
        ProcessExitResult exitResult, CancellationToken ct)
    {
        // No additional Linux-specific post-processing at this time.
        return Task.CompletedTask;
    }

    private static bool TryInstallProcdump()
    {
        var distro = DetectDistro();
        if (!DistroPackageMap.TryGetValue(distro, out var package))
        {
            GeneralTracer.Warn($"LinuxBowlStrategy: unsupported distro '{distro}', cannot install procdump.");
            return false;
        }

        var appDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Applications", "Linux");
        var scriptPath = Path.Combine(appDir, "install.sh");
        var packagePath = Path.Combine(appDir, package);

        if (!File.Exists(scriptPath))
        {
            GeneralTracer.Error($"LinuxBowlStrategy: install.sh not found at {scriptPath}.");
            return false;
        }

        if (!File.Exists(packagePath))
        {
            GeneralTracer.Error($"LinuxBowlStrategy: package not found at {packagePath}.");
            return false;
        }

        GeneralTracer.Info($"LinuxBowlStrategy: installing {package} via install.sh.");
        return RunInstallScript(scriptPath, packagePath);
    }

    private static bool RunInstallScript(string script, string package)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{script}\" \"{package}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            // Read stderr asynchronously to avoid pipe buffer deadlock
            var stderrTask = process.StandardError.ReadToEndAsync();

            var exited = process.WaitForExit(60_000);
            if (!exited)
            {
                process.Kill();
                GeneralTracer.Error("LinuxBowlStrategy: install script timed out.");
                return false;
            }

            if (process.ExitCode == 0)
            {
                GeneralTracer.Info("LinuxBowlStrategy: procdump installed successfully.");
                return true;
            }

            var stderr = stderrTask.Result;
            GeneralTracer.Error(
                $"LinuxBowlStrategy: install script failed with exit code {process.ExitCode}. stderr: {stderr}");
            return false;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LinuxBowlStrategy: exception running install script.", ex);
            return false;
        }
    }

    private static string DetectDistro()
    {
        const string osReleasePath = "/etc/os-release";
        if (!File.Exists(osReleasePath))
        {
            GeneralTracer.Warn("LinuxBowlStrategy: /etc/os-release not found.");
            return string.Empty;
        }

        string distro = string.Empty;
        foreach (var line in File.ReadAllLines(osReleasePath))
        {
            if (line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
            {
                distro = line.Substring(3).Trim('"', '\'');
                break;
            }
        }

        GeneralTracer.Info($"LinuxBowlStrategy: detected distro='{distro}'.");
        return distro.ToLowerInvariant();
    }

    private static void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }
}
