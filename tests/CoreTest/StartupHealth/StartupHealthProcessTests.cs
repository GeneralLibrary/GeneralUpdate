using System.Diagnostics;
using System.ComponentModel;
using GeneralUpdate.Core.StartupHealth;

namespace CoreTest.StartupHealth;

public class StartupHealthProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "startup-process-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Child_explicitly_confirms_business_readiness()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        var host = new StartupHealthProcess(_root);
        var readinessFile = Path.Combine(_root, "business-ready");
        File.WriteAllText(readinessFile, "ready");
        using var child = host.Start(id, Probe("business-ready", readinessFile));
        Assert.Equal(StartupHealthStatus.Healthy, await host.WaitAsync(id, child));
        Assert.True(child.WaitForExit(15000));
        Assert.Equal(0, child.ExitCode);
        Assert.Equal(StartupHealthStatus.Healthy, new StartupHealthStore(_root).GetStatus(id));
    }

    [Fact]
    public async Task Failed_business_check_does_not_confirm()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        var host = new StartupHealthProcess(_root);
        var start = Probe("business-ready", Path.Combine(_root, "missing-ready-file"));
        start.RedirectStandardError = true;
        using var child = host.Start(id, start);
        Assert.Equal(StartupHealthStatus.ProcessExited, await host.WaitAsync(id, child));
        Assert.Contains("business readiness check failed", child.StandardError.ReadToEnd());
    }

    [Fact]
    public async Task Exit_without_confirmation_is_not_healthy()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        var host = new StartupHealthProcess(_root);
        using var child = host.Start(id, Probe("exit"));
        Assert.Equal(StartupHealthStatus.ProcessExited, await host.WaitAsync(id, child));
    }

    [Fact]
    public async Task Running_without_confirmation_times_out()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(2));
        var host = new StartupHealthProcess(_root);
        using var child = host.Start(id, Probe("wait"));
        try
        {
            Assert.Equal(StartupHealthStatus.TimedOut, await host.WaitAsync(id, child));
            Assert.False(child.HasExited);
            Assert.False(store.ConfirmHealthy(id));
        }
        finally { Stop(child); }
    }

    [Fact]
    public void Process_creation_failure_is_persisted()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        var host = new StartupHealthProcess(_root);
        Assert.Throws<Win32Exception>(() => host.Start(id,
            new ProcessStartInfo(Path.Combine(_root, "missing-executable")) { UseShellExecute = false }));
        Assert.Equal(StartupHealthStatus.Failed, new StartupHealthStore(_root).GetStatus(id));
    }

    [Fact]
    public void Terminal_attempt_cannot_be_started_again()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        Assert.True(store.ReportFailure(id, StartupHealthStatus.Failed));
        var host = new StartupHealthProcess(_root);
        Assert.Throws<InvalidOperationException>(() => host.Start(id, Probe("wait")));
        Assert.Equal(StartupHealthStatus.Failed, store.GetStatus(id));
    }

    [Fact]
    public void No_environment_means_monitoring_is_disabled()
    {
        var start = Probe("disabled");
        start.Environment.Remove(StartupHealthProcess.DirectoryVariable);
        start.Environment.Remove(StartupHealthProcess.AttemptVariable);
        using var child = Process.Start(start)!;
        Assert.True(child.WaitForExit(15000));
        Assert.Equal(0, child.ExitCode);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Cancellation_does_not_change_the_attempt_or_kill_the_child()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(30));
        var host = new StartupHealthProcess(_root);
        using var child = host.Start(id, Probe("wait"));
        try
        {
            using var cts = new CancellationTokenSource(200);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.WaitAsync(id, child, cts.Token));
            Assert.Equal(StartupHealthStatus.Pending, store.GetStatus(id));
            Assert.False(child.HasExited);
        }
        finally { Stop(child); }
    }

    [Fact]
    public void Killed_host_can_reopen_attempt_without_inventing_an_exit()
    {
        var store = new StartupHealthStore(_root);
        var id = store.Begin(DateTimeOffset.UtcNow.AddSeconds(15));
        var pidFile = Path.Combine(_root, "child-pid");
        var start = Probe("host", _root, id.ToString("D"), pidFile, ProbePath(),
            RuntimeConfigPath(), DepsPath());
        using var host = Process.Start(start)!;
        Process? child = null;
        try
        {
            Assert.True(SpinWait.SpinUntil(() => File.Exists(pidFile) || host.HasExited, TimeSpan.FromSeconds(10)));
            Assert.True(File.Exists(pidFile), "Host exited before publishing the child PID.");
            child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
            Stop(host);
            Assert.Equal("Pending", ReadStatusInNewProcess(id));
            Assert.False(child.HasExited);
            Assert.True(SpinWait.SpinUntil(
                () => new StartupHealthStore(_root).GetStatus(id) == StartupHealthStatus.TimedOut,
                TimeSpan.FromSeconds(20)));
            Assert.Equal("TimedOut", ReadStatusInNewProcess(id));
        }
        finally
        {
            if (child != null) { Stop(child); child.Dispose(); }
            Stop(host);
        }
    }

    private static ProcessStartInfo Probe(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(RuntimeConfigPath());
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(DepsPath());
        start.ArgumentList.Add(ProbePath());
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    private string ReadStatusInNewProcess(Guid id)
    {
        var start = Probe(_root, id.ToString("D"), "status");
        start.RedirectStandardOutput = true;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(15000));
        Assert.Equal(0, process.ExitCode);
        return output.Trim();
    }

    private static string ProbePath() => Path.Combine(AppContext.BaseDirectory, "StartupHealthProbe.dll");
    private static string RuntimeConfigPath() => Path.Combine(AppContext.BaseDirectory, "CoreTest.runtimeconfig.json");
    private static string DepsPath() => Path.Combine(AppContext.BaseDirectory, "CoreTest.deps.json");

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
            process.WaitForExit();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
