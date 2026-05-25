using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;

namespace CoreTest.Download;

public class DownloadPlanBuilderTests
{
    private static DownloadAsset Asset(string name = "a", string version = "2.0.0", string url = "http://u",
        long size = 100, string hash = null, bool isFreeze = false, bool isForcibly = false,
        bool isCrossVersion = false, string fromVersion = null, string minClientVersion = null)
        => new(name, url, size, hash, version,
              IsFreeze: isFreeze, IsForcibly: isForcibly,
              IsCrossVersion: isCrossVersion, FromVersion: fromVersion,
              MinClientVersion: minClientVersion);

    [Fact]
    public void Build_AssetsNull_ReturnsEmpty()
    {
        var result = DownloadPlanBuilder.Build(null, "1.0.0");
        Assert.False(result.HasAssets);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    public void Build_CurrentVersionInvalid_ReturnsEmpty(string version)
    {
        var result = DownloadPlanBuilder.Build(new[] { Asset("a", "2.0.0") }, version);
        Assert.False(result.HasAssets);
    }

    [Fact]
    public void Build_AllAssetsFrozen_ReturnsEmpty()
    {
        var assets = new[]
        {
            Asset("a", "2.0.0", isFreeze: true),
            Asset("b", "3.0.0", isFreeze: true)
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.False(result.HasAssets);
    }

    [Fact]
    public void Build_SingleAssetVersionAboveCurrent_HasAssets()
    {
        var assets = new[] { Asset("update", "2.0.0") };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.HasAssets);
        Assert.Single(result.Assets);
    }

    [Fact]
    public void Build_AllVersionsBelowOrEqual_ReturnsEmpty()
    {
        var assets = new[] { Asset("old", "1.0.0"), Asset("older", "0.9.0") };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.False(result.HasAssets);
    }

    [Fact]
    public void Build_AnyAssetIsForcibly_IsForciblyTrue()
    {
        var assets = new[]
        {
            Asset("a", "2.0.0"),
            Asset("b", "2.1.0", isForcibly: true)
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.IsForcibly);
    }

    [Fact]
    public void Build_NoAssetIsForcibly_IsForciblyFalse()
    {
        var assets = new[] { Asset("a", "2.0.0") };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.False(result.IsForcibly);
    }

    [Fact]
    public void Build_CrossVersionMatch_ReturnsSingleAssetPlan()
    {
        var assets = new[]
        {
            Asset("cross", "5.0.0", isCrossVersion: true, fromVersion: "1.0.0"),
            Asset("inc", "2.0.0"), Asset("inc2", "3.0.0")
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.HasAssets);
        Assert.Single(result.Assets);
        Assert.Equal("5.0.0", result.Assets[0].Version);
    }

    [Fact]
    public void Build_MinClientVersionTooHigh_FilteredOut()
    {
        var assets = new[]
        {
            Asset("high", "3.0.0", minClientVersion: "2.0.0")
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.False(result.HasAssets);
    }

    [Fact]
    public void Build_MinClientVersionOk_Included()
    {
        var assets = new[]
        {
            Asset("ok", "3.0.0", minClientVersion: "1.0.0")
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.HasAssets);
    }

    [Fact]
    public void Build_ChainOrderedByVersionAscending()
    {
        var assets = new[]
        {
            Asset("c", "3.0.0"), Asset("a", "1.1.0"), Asset("b", "2.0.0")
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Equal(3, result.Assets.Count);
        Assert.Equal("1.1.0", result.Assets[0].Version);
        Assert.Equal("2.0.0", result.Assets[1].Version);
        Assert.Equal("3.0.0", result.Assets[2].Version);
    }

    [Fact]
    public void Build_MixedFrozenAndActive_FiltersFrozen()
    {
        var assets = new[]
        {
            Asset("active", "2.0.0"),
            Asset("frozen", "3.0.0", isFreeze: true)
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Single(result.Assets);
        Assert.Equal("2.0.0", result.Assets[0].Version);
    }

    [Fact]
    public void MapToAsset_NullFields_HasSaneDefaults()
    {
        var packet = new GeneralUpdate.Core.Download.Abstractions.PacketDTO
        {
            Name = null, Url = null, Version = null, Hash = null
        };
        var asset = DownloadPlanBuilder.MapToAsset(packet);
        Assert.Equal("unknown", asset.Name);
        Assert.Equal("", asset.Url);
        Assert.Equal("0.0.0", asset.Version);
        Assert.Equal(0, asset.Size);
    }
}
