using System.Diagnostics;
using System.Runtime.Versioning;

namespace CoreTest.Ipc;

/// <summary>Demonstrates loader search behavior, not a protected updater launcher.</summary>
public sealed class WindowsDllSearchExperimentTests
{
    [Fact]
    public async Task SanitizedPathDoesNotRestrictExplicitDllPaths()
    {
        if (!OperatingSystem.IsWindows()) return;
        await RunExperiment();
    }

    [Fact]
    public async Task NativeAotSqliteUsesAdjacentDependencyAndFailsWhenItIsMissing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var executable = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER");
        if (executable == null) return;
        await RunSqliteExperiment(executable);
    }

    [SupportedOSPlatform("windows")]
    private static async Task RunSqliteExperiment(string executable)
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-sqlite-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var adjacent = await RunSqlitePeer(executable, root);
            Assert.True(adjacent.ExitCode == 0, adjacent.Error);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(executable)!, "e_sqlite3.dll"),
                adjacent.Output.Trim(), ignoreCase: true);

            var isolated = Path.Combine(root, "peer.exe");
            File.Copy(executable, isolated);
            var missing = await RunSqlitePeer(isolated, root);
            Assert.NotEqual(0, missing.ExitCode);
            Assert.Contains("e_sqlite3", missing.Error + missing.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunSqlitePeer(string executable, string directory)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["PATH"] = Environment.SystemDirectory;
        start.ArgumentList.Add("--sqlite-module-probe");
        using var peer = Process.Start(start) ?? throw new InvalidOperationException("SQLite probe did not start.");
        var output = peer.StandardOutput.ReadToEndAsync();
        var error = peer.StandardError.ReadToEndAsync();
        try
        {
            await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return (peer.ExitCode, await output, await error);
        }
        finally
        {
            if (!peer.HasExited) peer.Kill(entireProcessTree: true);
            await peer.WaitForExitAsync();
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task RunExperiment()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-dll-search-" + Guid.NewGuid().ToString("N"));
        var injection = Path.Combine(root, "injection");
        var workingDirectory = Path.Combine(root, "working");
        Directory.CreateDirectory(injection);
        Directory.CreateDirectory(workingDirectory);
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "e_sqlite3.dll");
            var name = "gu_probe_" + Guid.NewGuid().ToString("N") + ".dll";
            var candidate = Path.Combine(injection, name);
            File.Copy(source, candidate);
            var executable = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER")
                ?? Path.Combine(AppContext.BaseDirectory, "ReportWorker.exe");

            Assert.Equal("loaded", await RunPeer(executable, workingDirectory, injection, name));
            Assert.Equal("error:126", await RunPeer(executable, workingDirectory, Environment.SystemDirectory, name));
            Assert.Equal("loaded", await RunPeer(executable, workingDirectory, Environment.SystemDirectory, candidate));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string?> RunPeer(string executable, string workingDirectory, string path, string dll)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["PATH"] = path;
        start.ArgumentList.Add("--dll-search-probe");
        start.ArgumentList.Add(dll);
        using var peer = Process.Start(start) ?? throw new InvalidOperationException("DLL search probe did not start.");
        var output = peer.StandardOutput.ReadToEndAsync();
        var error = peer.StandardError.ReadToEndAsync();
        try
        {
            await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(peer.ExitCode == 0, await error);
            return (await output).Trim();
        }
        finally
        {
            if (!peer.HasExited) peer.Kill(entireProcessTree: true);
            await peer.WaitForExitAsync();
        }
    }
}
