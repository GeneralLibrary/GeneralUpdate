using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Linux crash surveillance strategy using procdump.
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

    private bool _probed;
    private bool _procdumpAvailable;
    private string? _lastFailReason;

    public ProcessStartInfo? Prepare(in BowlContext context)
    {
        if (!_probed)
        {
            _procdumpAvailable = ProbeProcdump(context.FailDirectory);
            _probed = true;
        }

        if (!_procdumpAvailable)
            return null;

        var dumpFullPath = Path.Combine(context.FailDirectory, context.DumpFileName);
        EnsureDirectory(context.FailDirectory);

        // Detect whether the target is a PID (all digits) or a process name.
        // Linux procdump uses -p for PID and -w for process name.
        var isPid = long.TryParse(context.ProcessNameOrId, out _);
        var flag = isPid ? "-p" : "-w";

        GeneralTracer.Info(
            $"LinuxBowlStrategy.Prepare: target='{context.ProcessNameOrId}' ({(isPid ? "PID" : "name")}), " +
            $"flag={flag}, dump={dumpFullPath}");

        return new ProcessStartInfo
        {
            FileName = "procdump",
            Arguments = $"{flag} {context.ProcessNameOrId} -o \"{dumpFullPath}\"",
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

    // ---- Probe: check if procdump is available, install if not ----

    /// <summary>
    /// Returns true if procdump can be used.
    /// Writes a diagnostic file when the environment is unsupported.
    /// </summary>
    private bool ProbeProcdump(string failDirectory)
    {
        // 1. Already in PATH?
        if (IsProcdumpInPath())
        {
            GeneralTracer.Info("LinuxBowlStrategy: procdump found in PATH, skipping install.");
            return true;
        }

        // 2. Detect distro
        var distro = DetectDistro();
        if (string.IsNullOrEmpty(distro))
        {
            _lastFailReason = "Cannot detect Linux distribution: /etc/os-release not found.";
            WriteUnsupportedHint(failDirectory, _lastFailReason);
            GeneralTracer.Warn($"LinuxBowlStrategy: {_lastFailReason}");
            return false;
        }

        // 3. Check if we have a matching package
        if (!DistroPackageMap.TryGetValue(distro, out var package))
        {
            _lastFailReason =
                $"Unsupported Linux distribution: '{distro}'. " +
                $"Supported distributions: {string.Join(", ", DistroPackageMap.Keys)}.";
            WriteUnsupportedHint(failDirectory, _lastFailReason);
            GeneralTracer.Warn($"LinuxBowlStrategy: {_lastFailReason}");
            return false;
        }

        // 4. Locate bundled package and install script
        var appDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Applications", "Linux");
        var scriptPath = Path.Combine(appDir, "install.sh");
        var packagePath = Path.Combine(appDir, package);

        if (!File.Exists(scriptPath))
        {
            _lastFailReason = $"install.sh not found at {scriptPath}.";
            WriteUnsupportedHint(failDirectory, _lastFailReason);
            GeneralTracer.Error($"LinuxBowlStrategy: {_lastFailReason}");
            return false;
        }

        if (!File.Exists(packagePath))
        {
            _lastFailReason = $"procdump package not found at {packagePath}.";
            WriteUnsupportedHint(failDirectory, _lastFailReason);
            GeneralTracer.Error($"LinuxBowlStrategy: {_lastFailReason}");
            return false;
        }

        // 5. Run install script
        GeneralTracer.Info($"LinuxBowlStrategy: installing {package} via install.sh for distro '{distro}'.");
        var installed = RunInstallScript(scriptPath, packagePath);
        if (!installed)
        {
            _lastFailReason =
                $"Failed to install procdump package '{package}' for distribution '{distro}'. " +
                "Check that sudo is available without an interactive password prompt.";
            WriteUnsupportedHint(failDirectory, _lastFailReason);
            GeneralTracer.Error($"LinuxBowlStrategy: {_lastFailReason}");
            return false;
        }

        GeneralTracer.Info("LinuxBowlStrategy: procdump installed successfully.");
        return true;
    }

    // ---- Helpers ----

    private static bool IsProcdumpInPath()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/which",
                    Arguments = "procdump",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            var exited = process.WaitForExit(5000);
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

    private static void WriteUnsupportedHint(string failDirectory, string reason)
    {
        try
        {
            Directory.CreateDirectory(failDirectory);
            var hintPath = Path.Combine(failDirectory, "bowl_linux_unsupported.txt");
            var content =
                $"Bowl Linux Strategy — Unsupported Environment\n" +
                $"================================================\n" +
                $"Reason: {reason}\n" +
                $"Timestamp: {DateTime.UtcNow:O}\n" +
                $"\n" +
                $"Supported distributions: {string.Join(", ", DistroPackageMap.Keys)}\n" +
                $"\n" +
                $"To use Bowl on this system, install procdump manually and ensure it is\n" +
                $"available in PATH. Bowl will skip the automatic install if procdump\n" +
                $"is already present.\n";
            File.WriteAllText(hintPath, content);
            GeneralTracer.Info($"LinuxBowlStrategy: unsupported hint written to {hintPath}.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LinuxBowlStrategy: failed to write unsupported hint.", ex);
        }
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
        // Create the fail directory if it does not yet exist.
        // Do NOT delete an existing directory — that would destroy
        // crash diagnostics from previous surveillance sessions.
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
