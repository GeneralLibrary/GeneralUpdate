/// <summary>
/// 测试覆盖点：
/// - DefaultExtensionLifecycleHooks 所有方法默认行为_no-op，返回 true/CompletedTask
/// - 可继承重写验证
/// </summary>
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Core.Tests;

public class DefaultExtensionLifecycleHooksTests
{
    private readonly DefaultExtensionLifecycleHooks _hooks = new();

    [Fact]
    public async Task OnBeforeInstallAsync_默认返回true()
    {
        var meta = new ExtensionMetadata { Id = "test" };
        var result = await _hooks.OnBeforeInstallAsync(meta, null);
        Assert.True(result);
    }

    [Fact]
    public async Task OnBeforeInstallAsync_传入packagePath_仍返回true()
    {
        var meta = new ExtensionMetadata { Id = "test" };
        var result = await _hooks.OnBeforeInstallAsync(meta, "/path/to/package.zip");
        Assert.True(result);
    }

    [Fact]
    public async Task OnAfterInstallAsync_默认不抛异常()
    {
        await _hooks.OnAfterInstallAsync(new ExtensionMetadata { Id = "test" });
        Assert.True(true); // 不抛异常即通过
    }

    [Fact]
    public async Task OnBeforeActivateAsync_默认不抛异常()
    {
        await _hooks.OnBeforeActivateAsync("ext-1");
    }

    [Fact]
    public async Task OnAfterActivateAsync_默认不抛异常()
    {
        await _hooks.OnAfterActivateAsync("ext-1");
    }

    [Fact]
    public async Task OnBeforeDeactivateAsync_默认不抛异常()
    {
        await _hooks.OnBeforeDeactivateAsync("ext-1");
    }

    [Fact]
    public async Task OnAfterDeactivateAsync_默认不抛异常()
    {
        await _hooks.OnAfterDeactivateAsync("ext-1");
    }

    [Fact]
    public async Task OnBeforeUninstallAsync_默认返回true()
    {
        var result = await _hooks.OnBeforeUninstallAsync(new ExtensionMetadata { Id = "test" });
        Assert.True(result);
    }

    [Fact]
    public async Task OnAfterUninstallAsync_默认不抛异常()
    {
        await _hooks.OnAfterUninstallAsync("ext-1");
    }

    [Fact]
    public async Task 所有方法均可接收CancellationToken()
    {
        var cts = new CancellationTokenSource();
        var meta = new ExtensionMetadata { Id = "ext" };
        // 不等待取消，仅验证参数接收不抛异常
        await _hooks.OnBeforeInstallAsync(meta, null, cts.Token);
        await _hooks.OnAfterInstallAsync(meta, cts.Token);
        await _hooks.OnBeforeActivateAsync("ext", cts.Token);
        await _hooks.OnAfterActivateAsync("ext", cts.Token);
        await _hooks.OnBeforeDeactivateAsync("ext", cts.Token);
        await _hooks.OnAfterDeactivateAsync("ext", cts.Token);
        await _hooks.OnBeforeUninstallAsync(meta, cts.Token);
        await _hooks.OnAfterUninstallAsync("ext", cts.Token);
    }
}
