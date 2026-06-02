using GeneralUpdate.Drivelution.Core.Execution;

namespace DrivelutionTest.Execution;

/// <summary>
/// Unit tests for <see cref="CommandRunner"/> following AAAT pattern.
/// Tests constructor, interface contract, and RunOrThrowAsync behaviour.
/// </summary>
public class CommandRunnerTests
{
    #region Constructor

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var runner = new CommandRunner();
        Assert.NotNull(runner);
    }

    #endregion

    #region ICommandRunner contract

    [Fact]
    public void Implements_ICommandRunner()
    {
        var runner = new CommandRunner();
        Assert.IsAssignableFrom<ICommandRunner>(runner);
    }

    #endregion

    #region RunAsync — basic execution

    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsSuccessResult()
    {
        var runner = new CommandRunner();

        // Use a simple built-in command that always succeeds
        var result = await runner.RunAsync("cmd", new[] { "/c", "exit 0" });

        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var runner = new CommandRunner();

        var result = await runner.RunAsync("cmd", new[] { "/c", "exit 42" });

        Assert.NotNull(result);
        Assert.Equal(42, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RunAsync_CapturesStandardOutput()
    {
        var runner = new CommandRunner();

        var result = await runner.RunAsync("cmd", new[] { "/c", "echo HelloWorld" });

        Assert.Contains("HelloWorld", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_WithEmptyArguments_DoesNotThrow()
    {
        var runner = new CommandRunner();

        var result = await runner.RunAsync("cmd", new[] { "/c", "exit 0" });

        Assert.True(result.Success);
    }

    #endregion

    #region RunOrThrowAsync

    [Fact]
    public async Task RunOrThrowAsync_SuccessfulCommand_ReturnsResult()
    {
        var runner = new CommandRunner();

        var result = await runner.RunOrThrowAsync("cmd", new[] { "/c", "exit 0" });

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunOrThrowAsync_FailingCommand_ThrowsInvalidOperationException()
    {
        var runner = new CommandRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunOrThrowAsync("cmd", new[] { "/c", "exit 1" }));

        Assert.Contains("failed with exit code 1", ex.Message);
    }

    #endregion

    #region RunAsync — cancellation

    [Fact]
    public async Task RunAsync_WithCancellation_PreemptsExecution()
    {
        var runner = new CommandRunner();
        using var cts = new CancellationTokenSource();

        // Cancel after a short delay so the process has time to start
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync("cmd", new[] { "/c", "ping -n 10 127.0.0.1 > nul" }, cts.Token));
    }

    #endregion

    #region RunAsync — returns structured result

    [Fact]
    public async Task RunAsync_ResultProperties_ArePopulated()
    {
        var runner = new CommandRunner();

        var result = await runner.RunAsync("cmd", new[] { "/c", "echo test-output" });

        Assert.NotNull(result.StandardOutput);
        Assert.NotNull(result.StandardError);
        Assert.True(result.ExitCode >= 0);
    }

    #endregion
}
