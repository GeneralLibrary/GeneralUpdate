using System.Diagnostics;

namespace CoreTest.Ipc;

/// <summary>Exercises job lifetime behavior, not a protected updater launcher.</summary>
public sealed class WindowsJobLifetimeExperimentTests
{
    [Fact]
    public async Task InvalidJobIsRejectedBeforeChildRuns()
    {
        if (!OperatingSystem.IsWindows()) return;
        var executable = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER")
            ?? Path.Combine(AppContext.BaseDirectory, "ReportWorker.exe");
        var root = Path.Combine(Path.GetTempPath(), "gu-invalid-job-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var marker = Path.Combine(root, "started.txt");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--job-invalid-probe");
            start.ArgumentList.Add(executable);
            start.ArgumentList.Add(marker);
            using var owner = Process.Start(start) ?? throw new InvalidOperationException("Job owner did not start.");
            var output = owner.StandardOutput.ReadToEndAsync();
            var errors = owner.StandardError.ReadToEndAsync();
            try
            {
                await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(owner.ExitCode == 0, await errors);
                Assert.StartsWith("rejected:", await output);
                Assert.False(File.Exists(marker));
            }
            finally
            {
                if (!owner.HasExited) owner.Kill();
                await owner.WaitForExitAsync();
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JobListStopsChildWhenOwnerExits(bool killOwner)
    {
        if (!OperatingSystem.IsWindows()) return;
        var executable = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER")
            ?? Path.Combine(AppContext.BaseDirectory, "ReportWorker.exe");
        var root = Path.Combine(Path.GetTempPath(), "gu-job-lifetime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var marker = Path.Combine(root, "started.txt");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--job-owner-probe");
            start.ArgumentList.Add(executable);
            start.ArgumentList.Add(marker);
            using var owner = Process.Start(start) ?? throw new InvalidOperationException("Job owner did not start.");
            var errors = owner.StandardError.ReadToEndAsync();
            Process? child = null;
            try
            {
                var line = await owner.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                if (!int.TryParse(line, out var childId))
                    throw new InvalidOperationException($"Job owner did not report a child: {line}; {await errors}");
                child = Process.GetProcessById(childId);
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!File.Exists(marker) && !child.HasExited && DateTime.UtcNow < deadline)
                    await Task.Delay(50);
                Assert.True(File.Exists(marker), "The child did not run.");
                Assert.False(child.HasExited);
                if (killOwner) owner.Kill();
                else await owner.StandardInput.WriteLineAsync("release");
                await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                if (!killOwner) Assert.True(owner.ExitCode == 0, await errors);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(child.HasExited);
            }
            finally
            {
                if (!owner.HasExited) owner.Kill();
                await owner.WaitForExitAsync();
                if (child != null)
                {
                    if (!child.HasExited) child.Kill();
                    await child.WaitForExitAsync();
                    child.Dispose();
                }
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
