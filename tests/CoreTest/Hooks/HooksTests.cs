using System;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using Xunit;

namespace CoreTest.Hooks;

public class HooksTests
{
    [Fact]
    public async Task NoOpUpdateHooks_AllMethods_ReturnDefaults()
    {
        var hooks = new NoOpUpdateHooks();
        var ctx = new HookContext("test", "/tmp", "1.0", "2.0", AppType.Client);

        Assert.True(await hooks.OnBeforeUpdateAsync(ctx));
        await hooks.OnDownloadCompletedAsync(new("a", "1.0", 100, TimeSpan.Zero, null, true));
        await hooks.OnAfterUpdateAsync(ctx);
        await hooks.OnUpdateErrorAsync(ctx, new Exception("test"));
        await hooks.OnBeforeStartAppAsync(ctx);
    }

    [Fact]
    public void UpdateContext_RecordEquality_Works()
    {
        var a = new HookContext("app", "/path", "1.0", "2.0", AppType.Client);
        var b = new HookContext("app", "/path", "1.0", "2.0", AppType.Client);
        var c = new HookContext("app2", "/path", "1.0", "2.0", AppType.Client);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void DownloadContext_RecordEquality_Works()
    {
        var a = new DownloadContext("asset", "1.0", 100, TimeSpan.FromSeconds(1), "/tmp/a.zip", true);
        var b = new DownloadContext("asset", "1.0", 100, TimeSpan.FromSeconds(1), "/tmp/a.zip", true);
        var c = a with { AssetName = "other" };

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
