using System.Reflection;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;

namespace CoreTest.Bootstrap;

public sealed class BowlCrashCollectionOptionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RuntimeOption_ConfiguresMonitorWithoutChangingRelaunch(bool enabled, bool relaunch)
    {
        var bootstrap = new GeneralUpdateBootstrap();
        var context = new UpdateContext { LaunchClientAfterUpdate = relaunch };
        typeof(GeneralUpdateBootstrap).GetField("_configInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(bootstrap, context);
        bootstrap.SetOption(Option.BowlCrashCollectionEnabled, enabled);
        typeof(GeneralUpdateBootstrap).GetMethod("ApplyRuntimeOptions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(bootstrap, null);

        Assert.Equal(relaunch, context.LaunchClientAfterUpdate);
        if (!enabled)
        {
            Assert.Null(context.Monitoring);
            return;
        }
        Assert.True(context.Monitoring!.Enabled);
        Assert.True(context.Monitoring.CrashCollectionEnabled);
        Assert.Equal(@"C:\Bowl\Bowl.Host.exe", context.Monitoring.ExecutablePath);

    }

    [Fact]
    public void RuntimeOption_PreservesConfiguredHostPath()
    {
        var bootstrap = new GeneralUpdateBootstrap();
        var context = new UpdateContext { Monitoring = new BowlOptions { ExecutablePath = @"C:\Custom\Bowl.Host.exe" } };
        typeof(GeneralUpdateBootstrap).GetField("_configInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(bootstrap, context);
        bootstrap.SetOption(Option.BowlCrashCollectionEnabled, true);
        typeof(GeneralUpdateBootstrap).GetMethod("ApplyRuntimeOptions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(bootstrap, null);

        Assert.Equal(@"C:\Custom\Bowl.Host.exe", context.Monitoring!.ExecutablePath);
    }
}
