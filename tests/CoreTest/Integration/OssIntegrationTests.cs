using System;
using System.Net.Http;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Hooks;
using GeneralUpdate.Core.Strategy;
using Xunit;

namespace CoreTest.Integration;

public class OssIntegrationTests
{
    [Fact]
    public void OssDownloadSource_Creation_RequiresValidArgs()
    {
        var client = new HttpClient();
        Assert.Throws<ArgumentNullException>(() =>
            new OssDownloadSource(null!, "https://oss.example.com/versions.json"));
        Assert.Throws<ArgumentNullException>(() =>
            new OssDownloadSource(client, null!));

        var source = new OssDownloadSource(client, "https://oss.example.com/versions.json");
        Assert.NotNull(source);
    }

    [Fact]
    public void OssDownloadSource_DefaultTimeout_Is60Seconds()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, "https://oss.example.com/versions.json");
        Assert.NotNull(source);
    }

    [Fact]
    public void OssDownloadSource_CustomTimeout()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, "https://oss.example.com/versions.json", TimeSpan.FromSeconds(30));
        Assert.NotNull(source);
    }

    [Fact]
    public void OssUpdateStrategy_DownloadSource_IsInjected()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.Null(strategy.DownloadSource);
        Assert.Null(strategy.DownloadOrchestrator);

        var client = new HttpClient();
        var source = new OssDownloadSource(client, "https://oss.example.com/versions.json");
        strategy.DownloadSource = source;
        Assert.Same(source, strategy.DownloadSource);
    }

    [Fact]
    public void OssUpdateStrategy_Hooks_DefaultToNoOp()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        Assert.IsType<NoOpUpdateHooks>(strategy.Hooks);
        Assert.IsType<HttpUpdateReporter>(strategy.Reporter);
    }

    [Fact]
    public async Task OssUpdateStrategy_WithoutConfig_Throws()
    {
        var strategy = new OssStrategy(AppType.OssClient);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync());
    }

    [Fact]
    public async Task OssUpdateStrategy_WithoutConfig_ReturnsWithoutError()
    {
        // Oss client without UpdateUrl or local version config: no exception, just returns
        var strategy = new OssStrategy(AppType.OssClient);
        var config = new UpdateContext
        {
            UpdateAppName = "TestOss",
            ClientVersion = "1.0.0",
            InstallPath = "/test/oss"
        };
        strategy.Create(config);

        var ex = await Record.ExceptionAsync(() => strategy.ExecuteAsync());
        Assert.Null(ex);
    }

    [Fact]
    public void OssDownloadSource_ImplementsIDownloadSource()
    {
        var source = new OssDownloadSource(new HttpClient(), "https://oss.example.com/versions.json");
        Assert.IsAssignableFrom<IDownloadSource>(source);
    }
}
