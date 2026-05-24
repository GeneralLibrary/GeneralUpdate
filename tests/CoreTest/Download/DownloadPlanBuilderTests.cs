using System.Linq;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using Xunit;

namespace CoreTest.Download;

public class DownloadPlanBuilderTests
{
    [Fact]
    public void Build_EmptyAssets_ReturnsEmptyPlan()
    {
        var plan = DownloadPlanBuilder.Build(Array.Empty<DownloadAsset>(), "1.0.0");
        Assert.False(plan.HasAssets);
    }

    [Fact]
    public void Build_SingleAsset_ReturnsIt()
    {
        var assets = new[] { new DownloadAsset("pkg", "http://x", 100, "sha", "1.0.1") };
        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(plan.HasAssets);
        Assert.Single(plan.Assets);
        Assert.Equal("1.0.1", plan.Assets[0].Version);
    }

    [Fact]
    public void Build_CrossVersionPackage_DirectJump()
    {
        var assets = new[]
        {
            new DownloadAsset("chain", "http://x", 100, "sha", "1.0.1"),
            new DownloadAsset("jump", "http://y", 500, "sha2", "2.0.0",
                IsCrossVersion: true, FromVersion: "1.0.0")
        };

        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Single(plan.Assets); // Only the cross-version jump package
        Assert.Equal("2.0.0", plan.Assets[0].Version);
    }

    [Fact]
    public void Build_FrozenPackagesExcluded()
    {
        var assets = new[]
        {
            new DownloadAsset("bad", "http://x", 100, "sha", "1.0.1", IsFreeze: true),
            new DownloadAsset("good", "http://y", 100, "sha2", "1.0.2")
        };

        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Single(plan.Assets);
        Assert.Equal("1.0.2", plan.Assets[0].Version);
    }

    [Fact]
    public void Build_ForcedUpdate_MarksPlan()
    {
        var assets = new[]
        {
            new DownloadAsset("forced", "http://x", 100, "sha", "1.0.1", IsForcibly: true)
        };

        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(plan.IsForcibly);
    }

    [Fact]
    public void Build_VersionChain_MultipleSteps()
    {
        var assets = new[]
        {
            new DownloadAsset("v101", "http://x", 100, "sha1", "1.0.1"),
            new DownloadAsset("v102", "http://y", 100, "sha2", "1.0.2"),
            new DownloadAsset("v103", "http://z", 100, "sha3", "1.0.3")
        };

        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Equal(3, plan.Assets.Count);
        Assert.Equal("1.0.1", plan.Assets[0].Version);
        Assert.Equal("1.0.3", plan.Assets[^1].Version);
    }

    [Fact]
    public void Build_SameVersion_ReturnsEmpty()
    {
        var assets = new[] { new DownloadAsset("same", "http://x", 100, "sha", "1.0.0") };
        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.False(plan.HasAssets);
    }

    [Fact]
    public void Build_MinClientVersion_FiltersOut()
    {
        var assets = new[]
        {
            new DownloadAsset("compat", "http://x", 100, "sha1", "1.0.1", MinClientVersion: "1.0.0"),
            new DownloadAsset("incompat", "http://y", 100, "sha2", "1.0.2", MinClientVersion: "2.0.0")
        };

        var plan = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Single(plan.Assets);
        Assert.Equal("1.0.1", plan.Assets[0].Version);
    }
}
