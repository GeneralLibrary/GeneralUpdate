using System.Diagnostics;
using System.Runtime.CompilerServices;
using GeneralUpdate.Core.StartupHealth;

namespace CoreTest.StartupHealth;

public class StartupHealthStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "startup-health-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Explicit_confirmation_is_persistent_and_terminal()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(StartupHealthStatus.Pending, store.GetStatus(id));
        Assert.True(store.ConfirmHealthy(id));
        Assert.False(store.ConfirmHealthy(id));
        Assert.False(store.ReportFailure(id, StartupHealthStatus.Failed));
        Assert.Equal(StartupHealthStatus.Healthy, new StartupHealthStore(_root).GetStatus(id));
    }

    [Theory]
    [InlineData(StartupHealthStatus.Failed)]
    [InlineData(StartupHealthStatus.ProcessExited)]
    public void Failure_is_persistent_and_cannot_be_overwritten(StartupHealthStatus failure)
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(store.ReportFailure(id, failure));
        Assert.False(store.ConfirmHealthy(id));
        Assert.False(store.ReportFailure(id, failure));
        Assert.Equal(failure, new StartupHealthStore(_root).GetStatus(id));
    }

    [Fact]
    public void Deadline_is_enforced_even_without_a_running_monitor()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMilliseconds(200));
        Thread.Sleep(350);
        Assert.False(new StartupHealthStore(_root).ConfirmHealthy(id));
        Assert.Equal(StartupHealthStatus.TimedOut, store.GetStatus(id));
        Assert.False(store.ReportFailure(id, StartupHealthStatus.ProcessExited));
    }

    [Fact]
    public void Invalid_or_corrupt_state_never_becomes_success()
    {
        var store = new StartupHealthStore(_root);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Begin(DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Throws<ArgumentException>(() => store.GetStatus(Guid.Empty));
        Assert.Throws<DirectoryNotFoundException>(() => store.GetStatus(Guid.NewGuid()));
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ReportFailure(id, StartupHealthStatus.Healthy));
        File.WriteAllBytes(Path.Combine(_root, id.ToString("N"), "result"), new byte[] { 1 });
        Assert.Throws<InvalidDataException>(() => store.ConfirmHealthy(id));
    }

    [Fact]
    public void Interrupted_temporary_write_does_not_count_as_an_outcome()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        File.WriteAllBytes(Path.Combine(_root, id.ToString("N"), "result.interrupted.tmp"), new byte[] { 1 });
        Assert.Equal(StartupHealthStatus.Pending, store.GetStatus(id));
        Assert.True(store.ReportFailure(id, StartupHealthStatus.Failed));
        Assert.Equal(StartupHealthStatus.Failed, store.GetStatus(id));
    }

    [Fact]
    public void Competing_processes_commit_only_one_terminal_result()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        using var healthy = StartProbe(id, "healthy");
        using var failed = StartProbe(id, "failed");
        var healthyOutput = Finish(healthy);
        var failedOutput = Finish(failed);
        Assert.NotEqual(healthyOutput, failedOutput);
        Assert.Contains("changed", new[] { healthyOutput, failedOutput });
        Assert.Contains("unchanged", new[] { healthyOutput, failedOutput });
        Assert.Equal(healthyOutput == "changed" ? StartupHealthStatus.Healthy : StartupHealthStatus.Failed,
            store.GetStatus(id));
    }

    [Fact]
    public void Crashed_lock_holder_does_not_prevent_recovery()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
        var ready = Path.Combine(_root, "ready");
        using var process = StartProbe(id, "hold-lock", ready);
        try
        {
            Assert.True(SpinWait.SpinUntil(() => File.Exists(ready) || process.HasExited, TimeSpan.FromSeconds(15)));
            if (!File.Exists(ready))
                throw new InvalidOperationException("Probe exited before acquiring the lock: " + process.StandardError.ReadToEnd());
            var report = Task.Run(() => store.ReportFailure(id, StartupHealthStatus.ProcessExited));
            Assert.False(report.Wait(150));
            process.Kill();
            Assert.True(process.WaitForExit(15000));
            Assert.True(report.Wait(15000));
            Assert.True(report.Result);
            Assert.Equal(StartupHealthStatus.ProcessExited, new StartupHealthStore(_root).GetStatus(id));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
    }

    private Process StartProbe(Guid id, string operation, string? extra = null)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var helper = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..",
            "StartupHealthProbe", "bin", configuration, "net10.0", "StartupHealthProbe.dll"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(helper);
        start.ArgumentList.Add(_root);
        start.ArgumentList.Add(id.ToString());
        start.ArgumentList.Add(operation);
        if (extra != null) start.ArgumentList.Add(extra);
        return Process.Start(start) ?? throw new InvalidOperationException("Failed to start health probe.");
    }

    private static string Finish(Process process)
    {
        Assert.True(process.WaitForExit(15000));
        var error = process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, error);
        return process.StandardOutput.ReadToEnd().Trim();
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
