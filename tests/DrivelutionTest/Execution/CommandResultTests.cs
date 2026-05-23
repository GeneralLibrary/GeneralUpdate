using GeneralUpdate.Drivelution.Core.Execution;

namespace DrivelutionTest.Execution;

public class CommandResultTests
{
    [Fact]
    public void Success_WhenExitCodeZero_ReturnsTrue()
    {
        var result = new CommandResult { ExitCode = 0 };
        Assert.True(result.Success);
    }

    [Fact]
    public void Success_WhenExitCodeNonZero_ReturnsFalse()
    {
        var result = new CommandResult { ExitCode = 1 };
        Assert.False(result.Success);
    }

    [Fact]
    public void ToString_IncludesExitCode()
    {
        var result = new CommandResult { ExitCode = 0, StandardOutput = "hello" };
        Assert.Contains("0", result.ToString());
        Assert.Contains("hello", result.ToString());
    }
}
