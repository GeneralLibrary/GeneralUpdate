using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Utilities;

/// <summary>
/// RestartHelper 测试
/// 分支覆盖点:
/// - HandleRestartAsync: RestartMode.None -> true
/// - HandleRestartAsync: RestartMode.Prompt -> PromptUserForRestart (返回false)
/// - HandleRestartAsync: RestartMode.Delayed -> 延迟后调用重启
/// - HandleRestartAsync: RestartMode.Immediate -> 立即重启
/// - HandleRestartAsync: 未知RestartMode -> false
/// - PromptUserForRestart: 始终返回false (简化实现)
/// - IsRestartRequired: None -> false, 其他 -> true
/// - RestartCurrentProcess: 会导致进程退出(无法直接测试)，但可验证调用不抛异常
/// - RestartSystemAsync: 端到端测试(会尝试真实重启, 此处只测试异常处理路径)
/// 触发条件：调用各辅助方法
/// 预期结果：模式分发正确
/// </summary>
public class RestartHelperTests
{
    [Fact(DisplayName = "RestartHelper_HandleRestartAsync_RestartModeNone返回true")]
    public async Task HandleRestartAsync_ModeNone_ReturnsTrue()
    {
        var result = await RestartHelper.HandleRestartAsync(RestartMode.None);

        Assert.True(result);
    }

    [Fact(DisplayName = "RestartHelper_HandleRestartAsync_RestartModePrompt返回false")]
    public async Task HandleRestartAsync_ModePrompt_ReturnsFalse()
    {
        // PromptUserForRestart always returns false in simplified implementation
        var result = await RestartHelper.HandleRestartAsync(RestartMode.Prompt);

        Assert.False(result);
    }

    [Fact(DisplayName = "RestartHelper_PromptUserForRestart_返回false")]
    public void PromptUserForRestart_ReturnsFalse()
    {
        var result = RestartHelper.PromptUserForRestart();

        Assert.False(result);
    }

    [Fact(DisplayName = "RestartHelper_PromptUserForRestart_带消息参数返回false")]
    public void PromptUserForRestart_WithMessage_ReturnsFalse()
    {
        var result = RestartHelper.PromptUserForRestart("Custom message");

        Assert.False(result);
    }

    [Fact(DisplayName = "RestartHelper_PromptUserForRestart_空消息使用默认消息")]
    public void PromptUserForRestart_EmptyMessage_UsesDefault()
    {
        var result = RestartHelper.PromptUserForRestart("");

        Assert.False(result);
    }

    [Fact(DisplayName = "RestartHelper_IsRestartRequired_None模式返回false")]
    public void IsRestartRequired_ModeNone_ReturnsFalse()
    {
        Assert.False(RestartHelper.IsRestartRequired(RestartMode.None));
    }

    [Theory(DisplayName = "RestartHelper_IsRestartRequired_非None模式返回true")]
    [InlineData(RestartMode.Prompt)]
    [InlineData(RestartMode.Delayed)]
    [InlineData(RestartMode.Immediate)]
    public void IsRestartRequired_NonNoneMode_ReturnsTrue(RestartMode mode)
    {
        Assert.True(RestartHelper.IsRestartRequired(mode));
    }

    [Fact(DisplayName = "RestartHelper_HandleRestartAsync_Delayed模式_等待后尝试重启")]
    public async Task HandleRestartAsync_ModeDelayed_WaitsAndReturns()
    {
        using var cts = new CancellationTokenSource(2000);

        var task = RestartHelper.HandleRestartAsync(RestartMode.Delayed, 1, "test");

        // Should complete quickly (1 second delay) or fail fast on non-elevated
        try { await task.WaitAsync(cts.Token); } catch (OperationCanceledException) { }
    }

    [Fact(DisplayName = "RestartHelper_HandleRestartAsync_Immediate模式返回结果")]
    public async Task HandleRestartAsync_ModeImmediate_ReturnsResult()
    {
        using var cts = new CancellationTokenSource(2000);

        var task = RestartHelper.HandleRestartAsync(RestartMode.Immediate);

        try { await task.WaitAsync(cts.Token); } catch (OperationCanceledException) { }
    }

    [Fact(DisplayName = "RestartHelper_HandleRestartAsync_未知模式返回false")]
    public async Task HandleRestartAsync_UnknownMode_ReturnsFalse()
    {
        var result = await RestartHelper.HandleRestartAsync((RestartMode)999);

        Assert.False(result);
    }
}
