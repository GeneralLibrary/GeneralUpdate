using System;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using Xunit;

namespace CoreTest.Hooks;

public class HooksIntegrationTests
{
    [Fact]
    public async Task NoOpUpdateHooks_AllReturnDefault()
    {
        var hooks = new NoOpUpdateHooks();
        var ctx = new UpdateContext("TestApp", "/path", "1.0.0", "1.0.1", AppType.Client);

        var beforeResult = await hooks.OnBeforeUpdateAsync(ctx);
        Assert.True(beforeResult);

        var dcx = new DownloadContext("pkg.zip", "1.0.1", 1000, TimeSpan.FromSeconds(1), null, true);
        await hooks.OnDownloadCompletedAsync(dcx);
        await hooks.OnAfterUpdateAsync(ctx);
        await hooks.OnUpdateErrorAsync(ctx, new Exception("test"));
        await hooks.OnBeforeStartAppAsync(ctx);
    }

    [Fact]
    public async Task UnixPermissionHooks_BeforeStartApp_DoesNotThrow()
    {
        var hooks = new UnixPermissionHooks();
        var ctx = new UpdateContext("non_existent_app", "/tmp/test", "1.0.0", null, AppType.Client);

        await hooks.OnBeforeStartAppAsync(ctx);
    }

    [Fact]
    public void CustomPermissionHooks_RequiresScriptPath()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CustomPermissionHooks(null!));
    }

    [Fact]
    public void CustomPermissionHooks_StoresScriptPath()
    {
        var hooks = new CustomPermissionHooks("/usr/local/bin/my-script.sh");
        var ctx = new UpdateContext("app", "/path", "1.0.0", null, AppType.Client);

        // CustomPermissionHooks throws when script fails
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            hooks.OnBeforeStartAppAsync(ctx));
    }

    [Fact]
    public async Task CustomPermissionHooks_BeforeUpdate_Allows()
    {
        var hooks = new CustomPermissionHooks("/bin/true");
        var ctx = new UpdateContext("app", "/path", "1.0.0", "1.0.1", AppType.Client);

        var result = await hooks.OnBeforeUpdateAsync(ctx);
        Assert.True(result);
    }

    [Fact]
    public void UpdateContext_PropertiesSet()
    {
        var ctx = new UpdateContext("MyApp", "/opt/myapp", "1.0.0", "2.0.0", AppType.Client);

        Assert.Equal("MyApp", ctx.AppName);
        Assert.Equal("/opt/myapp", ctx.InstallPath);
        Assert.Equal("1.0.0", ctx.CurrentVersion);
        Assert.Equal("2.0.0", ctx.TargetVersion);
        Assert.Equal(AppType.Client, ctx.AppType);
    }

    [Fact]
    public void DownloadContext_PropertiesSet()
    {
        var ctx = new DownloadContext("pkg.zip", "1.0.1", 5000, TimeSpan.FromSeconds(3), "/tmp/pkg.zip", true);

        Assert.Equal("pkg.zip", ctx.AssetName);
        Assert.Equal("1.0.1", ctx.Version);
        Assert.Equal(5000, ctx.TotalBytes);
        Assert.Equal(TimeSpan.FromSeconds(3), ctx.Duration);
        Assert.Equal("/tmp/pkg.zip", ctx.LocalPath);
        Assert.True(ctx.Success);
    }
}
