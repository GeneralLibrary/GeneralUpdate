using System.Diagnostics;
using GeneralUpdate.Bowl.Strategies;

namespace BowlTest.Strategies;

/// <summary>
/// Unit tests for <see cref="ProcessRunner"/> following AAAT pattern.
/// Tests process execution, output capture, timeout, and cancellation.
/// </summary>
public class ProcessRunnerTests
{
    #region RunAsync — successful execution

    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsExitCodeZero()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c exit 0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await ProcessRunner.RunAsync(psi, timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_ReturnsCorrectExitCode()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c exit 7",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await ProcessRunner.RunAsync(psi, timeoutMs: 10000);

        Assert.Equal(7, result.ExitCode);
    }

    #endregion

    #region RunAsync — output capture

    [Fact]
    public async Task RunAsync_CapturesStandardOutput()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c echo ProcessRunnerTest",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await ProcessRunner.RunAsync(psi, timeoutMs: 10000);

        Assert.Contains(result.OutputLines, line => line.Contains("ProcessRunnerTest"));
    }

    [Fact]
    public async Task RunAsync_OutputLines_IsNotNull()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c echo hello",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await ProcessRunner.RunAsync(psi, timeoutMs: 10000);

        Assert.NotNull(result.OutputLines);
    }

    #endregion

    #region RunAsync — failed process start

    [Fact]
    public async Task RunAsync_NonExistentCommand_ThrowsException()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nonexistent_command_xyz_123.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        await Assert.ThrowsAnyAsync<Exception>(
            () => ProcessRunner.RunAsync(psi, timeoutMs: 5000));
    }

    #endregion

    #region RunAsync — timeout

    [Fact]
    public async Task RunAsync_Timeout_ThrowsTimeoutException()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c ping -n 30 127.0.0.1 > nul",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        await Assert.ThrowsAsync<TimeoutException>(
            () => ProcessRunner.RunAsync(psi, timeoutMs: 500));
    }

    #endregion

    #region RunAsync — cancellation

    [Fact]
    public async Task RunAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c ping -n 30 127.0.0.1 > nul",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var cts = new CancellationTokenSource(300);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ProcessRunner.RunAsync(psi, timeoutMs: 30000, cts.Token));
    }

    #endregion

    #region RunAsync — structured result

    [Fact]
    public async Task RunAsync_ResultHasOutputLines()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c echo line1 && echo line2",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await ProcessRunner.RunAsync(psi, timeoutMs: 10000);

        Assert.NotEmpty(result.OutputLines);
    }

    #endregion
}
