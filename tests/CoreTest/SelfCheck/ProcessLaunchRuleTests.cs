using System.Diagnostics;
using System.Runtime.InteropServices;
using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.SelfCheck;

/// <summary>
///     ProcessLaunchRule tests, using real child processes (per the design's test plan):
///     immediate non-zero exit → fail; alive past the window → pass (lenient); missing
///     executable → fail; strict RequireAck mode honours the verify-ok / verify-fail files.
/// </summary>
public sealed class ProcessLaunchRuleTests : IDisposable
{
    private readonly string _installPath;

    public ProcessLaunchRuleTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), $"gu-launch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installPath);
    }

    public void Dispose()
    {
        UpdateVerificationMarker.DeleteAckFiles(_installPath);
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
    }

    private static (string FileName, string Arguments) ImmediateExit(int code) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", $"/c exit {code}")
            : ("/bin/sh", $"-c \"exit {code}\"");

    private static (string FileName, string Arguments) StayAlive() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", "/c ping -n 60 127.0.0.1 > nul")
            : ("/bin/sh", "-c \"sleep 60\"");

    [Fact]
    public async Task ImmediateNonZeroExit_Fails()
    {
        var (fileName, arguments) = ImmediateExit(3);
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        var rule = new ProcessLaunchRule(process, TimeSpan.FromSeconds(15), requireAck: false);

        var result = await rule.CheckAsync(NewContext(), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains("3", result.Detail);
    }

    [Fact]
    public async Task ImmediateZeroExit_PassesWithDetail()
    {
        var (fileName, arguments) = ImmediateExit(0);
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        var rule = new ProcessLaunchRule(process, TimeSpan.FromSeconds(15), requireAck: false);

        var result = await rule.CheckAsync(NewContext(), CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Contains("code 0", result.Detail);
    }

    [Fact]
    public async Task AlivePastWindow_PassesLeniently()
    {
        var (fileName, arguments) = StayAlive();
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        var rule = new ProcessLaunchRule(process, TimeSpan.FromSeconds(1), requireAck: false);

        try
        {
            var result = await rule.CheckAsync(NewContext(), CancellationToken.None);

            Assert.True(result.Passed);
            Assert.Contains("alive", result.Detail);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task MissingExecutable_FailsWithoutProcess()
    {
        var rule = new ProcessLaunchRule(process: null, TimeSpan.FromSeconds(5), requireAck: false);

        var result = await rule.CheckAsync(NewContext(appName: "definitely-missing.exe"), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains("not found", result.Detail);
    }

    [Fact]
    public async Task RequireAck_VerifyFailFailsImmediately()
    {
        var (fileName, arguments) = StayAlive();
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        UpdateVerificationMarker.WriteAck(_installPath, healthy: false, "db down");
        var rule = new ProcessLaunchRule(process, TimeSpan.FromSeconds(15), requireAck: true);

        try
        {
            var result = await rule.CheckAsync(NewContext(), CancellationToken.None);

            Assert.False(result.Passed);
            Assert.Contains("db down", result.Detail);
            // The rule shut the residual process down before returning.
            await process.WaitForExitAsync();
            Assert.True(process.HasExited);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task RequireAck_VerifyOkPassesBeforeWindowElapses()
    {
        var (fileName, arguments) = StayAlive();
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        var rule = new ProcessLaunchRule(process, TimeSpan.FromSeconds(30), requireAck: true);

        try
        {
            // Ack arrives after the rule started observing — the polling loop must find it.
            _ = Task.Run(async () =>
            {
                await Task.Delay(300);
                UpdateVerificationMarker.WriteAck(_installPath, healthy: true, "healthy");
            });

            var result = await rule.CheckAsync(NewContext(), CancellationToken.None);

            Assert.True(result.Passed);
            Assert.Contains("verify-ok", result.Detail);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(); } catch { /* best effort */ }
        }
    }

    private SelfCheckContext NewContext(string? appName = null) => new(
        _installPath, appName ?? "app.exe", "1.0.2", "1.0.1", null,
        GeneralUpdate.Core.Configuration.AppType.Upgrade,
        UpdateVerificationMarker.GetPath(_installPath));
}
