using System.Diagnostics;
using GeneralUpdate.Core.StartupHealth;

namespace CoreTest.StartupHealth;

public class StartupHealthHandoffTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "startup-handoff-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Handoff_launches_child_and_requires_explicit_readiness()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30));
        var ready = Path.Combine(_root, "ready");
        File.WriteAllText(ready, "ready");
        using var child = handoff.Start(id, Probe("business-ready", ready));
        Assert.Equal(StartupHealthStatus.Healthy, await handoff.WaitAsync(id, child));
        Assert.True(child.WaitForExit(15000));
        Assert.Equal(0, child.ExitCode);
        Assert.Equal(StartupHealthStatus.Healthy, new StartupHealthHandoff(_root).GetStatus(id));
    }

    [Fact]
    public void Restart_after_prepare_can_start_but_cannot_recreate_attempt()
    {
        var id = Guid.NewGuid();
        var marker = Path.Combine(_root, "prepared");
        using var host = Process.Start(Probe("handoff-prepare", _root, id.ToString("D"), marker))!;
        try
        {
            Assert.True(SpinWait.SpinUntil(() => File.Exists(marker) || host.HasExited, TimeSpan.FromSeconds(15)));
            Assert.True(File.Exists(marker));
            Stop(host);
            var handoff = new StartupHealthHandoff(_root);
            Assert.Equal(StartupHealthStatus.Pending, handoff.GetStatus(id));
            Assert.Throws<IOException>(() => handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30)));
            using var child = handoff.Start(id, Probe("wait"));
            try { Assert.False(child.HasExited); }
            finally { Stop(child); }
        }
        finally { Stop(host); }
    }

    [Fact]
    public void Killed_launcher_cannot_be_relaunched_and_does_not_imply_exit()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30));
        var pidFile = Path.Combine(_root, "pid");
        using var host = Process.Start(Host(id, pidFile))!;
        Process? child = null;
        try
        {
            // The host creates the pid file before writing the PID, so the spin
            // must wait for parseable content, not just existence — otherwise a
            // fast reader observes an empty file and int.Parse throws.
            Assert.True(SpinWait.SpinUntil(() =>
            {
                if (host.HasExited) return true;
                try { return File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile), out _); }
                catch (IOException) { return false; }
            }, TimeSpan.FromSeconds(15)));
            Assert.True(File.Exists(pidFile));
            child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
            Stop(host);
            var recovered = new StartupHealthHandoff(_root);
            Assert.Equal(StartupHealthStatus.Pending, recovered.GetStatus(id));
            Assert.False(child.HasExited);
            Assert.Throws<IOException>(() => recovered.Start(id, Probe("wait")));
            Assert.Equal(StartupHealthStatus.Pending, recovered.GetStatus(id));
        }
        finally
        {
            if (child != null) { Stop(child); child.Dispose(); }
            Stop(host);
        }
    }

    [Fact]
    public void Interrupted_launch_claim_is_not_retried()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30));
        File.WriteAllBytes(Path.Combine(_root, id.ToString("N"), "launch"), Array.Empty<byte>());
        Assert.Throws<IOException>(() => new StartupHealthHandoff(_root).Start(id, Probe("wait")));
        Assert.Equal(StartupHealthStatus.Pending, handoff.GetStatus(id));
    }

    [Fact]
    public void Interrupted_creation_is_not_reinitialized()
    {
        var id = Guid.NewGuid();
        var attemptDir = Path.Combine(_root, id.ToString("N"));
        Directory.CreateDirectory(attemptDir);
        File.WriteAllBytes(Path.Combine(attemptDir, "creation"), Array.Empty<byte>());
        var handoff = new StartupHealthHandoff(_root);
        Assert.Throws<IOException>(() => handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30)));
        Assert.Throws<FileNotFoundException>(() => handoff.GetStatus(id));
    }

    [Fact]
    public void Restart_records_timeout_without_assuming_the_child_is_healthy()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddMilliseconds(200));
        Thread.Sleep(350);
        Assert.Equal(StartupHealthStatus.TimedOut, new StartupHealthHandoff(_root).GetStatus(id));
        Assert.Throws<InvalidOperationException>(() => handoff.Start(id, Probe("wait")));
    }

    [Fact]
    public void Failed_launch_is_persisted_and_cannot_be_retried()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30));
        Assert.Throws<System.ComponentModel.Win32Exception>(() => handoff.Start(id,
            new ProcessStartInfo(Path.Combine(_root, "missing")) { UseShellExecute = false }));
        Assert.Equal(StartupHealthStatus.Failed, new StartupHealthHandoff(_root).GetStatus(id));
        Assert.Throws<InvalidOperationException>(() => handoff.Start(id, Probe("wait")));
    }

    [Fact]
    public void Competing_launcher_processes_cannot_both_start()
    {
        var id = Guid.NewGuid();
        var handoff = new StartupHealthHandoff(_root);
        handoff.Prepare(id, DateTimeOffset.UtcNow.AddSeconds(30));
        var firstPid = Path.Combine(_root, "pid1");
        var secondPid = Path.Combine(_root, "pid2");
        using var first = Process.Start(Host(id, firstPid))!;
        using var second = Process.Start(Host(id, secondPid))!;
        Process? child = null;
        try
        {
            Assert.True(SpinWait.SpinUntil(
                () => (File.Exists(firstPid) && second.HasExited) || (File.Exists(secondPid) && first.HasExited),
                TimeSpan.FromSeconds(15)));
            Assert.NotEqual(File.Exists(firstPid), File.Exists(secondPid));
            var loser = File.Exists(firstPid) ? second : first;
            Assert.NotEqual(0, loser.ExitCode);
            child = Process.GetProcessById(int.Parse(File.ReadAllText(File.Exists(firstPid) ? firstPid : secondPid)));
            Assert.Equal(StartupHealthStatus.Pending, handoff.GetStatus(id));
        }
        finally
        {
            if (child != null) { Stop(child); child.Dispose(); }
            Stop(first);
            Stop(second);
        }
    }

    private ProcessStartInfo Host(Guid id, string pidFile) => Probe("handoff-host", _root, id.ToString("D"), pidFile,
        Path.Combine(AppContext.BaseDirectory, "StartupHealthProbe.dll"),
        Path.Combine(AppContext.BaseDirectory, "CoreTest.runtimeconfig.json"),
        Path.Combine(AppContext.BaseDirectory, "CoreTest.deps.json"));

    private static ProcessStartInfo Probe(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CoreTest.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CoreTest.deps.json"));
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "StartupHealthProbe.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
