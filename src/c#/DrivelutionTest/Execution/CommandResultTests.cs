using GeneralUpdate.Drivelution.Core.Execution;

namespace DrivelutionTest.Execution;

/// <summary>
/// CommandResult 测试
/// 分支覆盖点:
/// - 默认构造函数属性默认值
/// - Success 属性：ExitCode == 0 返回 true，ExitCode != 0 返回 false
/// - ExitCode 正整数/负整数边界
/// - StandardOutput/StandardError 字符串
/// - ToString：成功时显示 Output，失败时显示 Error
/// 触发条件：创建 CommandResult 并设置属性
/// 预期结果：逻辑正确
/// </summary>
public class CommandResultTests
{
    [Fact(DisplayName = "CommandResult_默认构造函数_所有属性为默认值")]
    public void CommandResult_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var result = new CommandResult();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact(DisplayName = "CommandResult_Success_ExitCode为0时返回true")]
    public void CommandResult_Success_ExitCodeZero_ReturnsTrue()
    {
        var result = new CommandResult { ExitCode = 0 };
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "CommandResult_Success_ExitCode为1时返回false")]
    public void CommandResult_Success_ExitCodeOne_ReturnsFalse()
    {
        var result = new CommandResult { ExitCode = 1 };
        Assert.False(result.Success);
    }

    [Theory(DisplayName = "CommandResult_Success_ExitCode不为零时全返回false")]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(255)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void CommandResult_Success_NonZeroExitCode_ReturnsFalse(int exitCode)
    {
        var result = new CommandResult { ExitCode = exitCode };
        Assert.False(result.Success);
    }

    [Fact(DisplayName = "CommandResult_ToString_成功时包含Output")]
    public void CommandResult_ToString_Success_ContainsOutput()
    {
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "Driver installed successfully\n"
        };

        var str = result.ToString();
        Assert.Contains("ExitCode=0", str);
        Assert.Contains("Driver installed successfully", str);
        Assert.DoesNotContain("Error=", str);
    }

    [Fact(DisplayName = "CommandResult_ToString_失败时包含Error")]
    public void CommandResult_ToString_Failure_ContainsError()
    {
        var result = new CommandResult
        {
            ExitCode = 1,
            StandardError = "Permission denied\n"
        };

        var str = result.ToString();
        Assert.Contains("ExitCode=1", str);
        Assert.Contains("Error=Permission denied", str);
    }

    [Fact(DisplayName = "CommandResult_ToString_StandardOutput有前后空白时被Trim")]
    public void CommandResult_ToString_StandardOutput_Trimmed()
    {
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "  output  \n"
        };

        var str = result.ToString();
        Assert.Contains("Output=output", str);
    }

    [Fact(DisplayName = "CommandResult_ToString_StandardError有前后空白时被Trim")]
    public void CommandResult_ToString_StandardError_Trimmed()
    {
        var result = new CommandResult
        {
            ExitCode = 2,
            StandardError = "  error  \n"
        };

        var str = result.ToString();
        Assert.Contains("Error=error", str);
    }

    [Fact(DisplayName = "CommandResult_ToString_Output为空字符串时正常处理")]
    public void CommandResult_ToString_EmptyOutput_Works()
    {
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = ""
        };

        var str = result.ToString();
        Assert.Contains("Output=", str);
    }

    [Fact(DisplayName = "CommandResult_ToString_Error为空字符串时正常处理")]
    public void CommandResult_ToString_EmptyError_Works()
    {
        var result = new CommandResult
        {
            ExitCode = 1,
            StandardError = ""
        };

        var str = result.ToString();
        Assert.Contains("Error=", str);
    }
}
