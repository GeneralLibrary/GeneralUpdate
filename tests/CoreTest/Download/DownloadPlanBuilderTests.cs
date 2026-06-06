using GeneralUpdate.Core.Configuration;
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
    public void Build_CrossVersionIncluded_CvpFirst_ReturnsCvpOnly()
    {
        // When a matching CVP exists, the plan selects the CVP and drops
        // same-AppType chain packages (CVP covers the full range).
        var assets = new[]
        {
            Asset("cross", "5.0.0", isCrossVersion: true, fromVersion: "1.0.0"),
            Asset("inc", "2.0.0"), Asset("inc2", "3.0.0")
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.HasAssets);
        Assert.Single(result.Assets);
        Assert.True(result.Assets[0].IsCrossVersion);
        Assert.Equal("5.0.0", result.Assets[0].Version);
    }

    [Fact]
    public void Build_CvpWithMixedAppTypes_KeepsChainForOtherTypes()
    {
        // CVP covers Client (AppType=1). Upgrade chain packages (AppType=2)
        // should still be included since the CVP doesn't cover that AppType.
        var assets = new[]
        {
            AssetWithType("cvp", "5.0.0", appType: 1, isCrossVersion: true, fromVersion: "1.0.0"),
            AssetWithType("upgrade1", "2.0.0", appType: 2),
            AssetWithType("upgrade2", "3.0.0", appType: 2),
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.True(result.HasAssets);
        Assert.Equal(3, result.Assets.Count);
        Assert.Equal("5.0.0", result.Assets[0].Version); // CVP first
        Assert.True(result.Assets[0].IsCrossVersion);
        Assert.Equal("2.0.0", result.Assets[1].Version); // chain for other AppType
        Assert.Equal("3.0.0", result.Assets[2].Version);
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

    // ════════════════════════════════════════════════════════════
    // HasUpdate — max server version vs local manifest version
    // ════════════════════════════════════════════════════════════

    private static DownloadAsset AssetWithType(string name = "a", string version = "2.0.0",
        string url = "http://u", long size = 100, string? hash = null,
        bool isFreeze = false, bool isForcibly = false,
        bool isCrossVersion = false, string? fromVersion = null,
        string? minClientVersion = null, int? appType = null)
        => new(name, url, size, hash, version,
              IsFreeze: isFreeze, IsForcibly: isForcibly,
              IsCrossVersion: isCrossVersion, FromVersion: fromVersion,
              MinClientVersion: minClientVersion, AppType: appType);

    [Fact]
    public void HasUpdate_EmptyAssets_ReturnsFalse()
    {
        var assets = Array.Empty<DownloadAsset>();
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "1.0.0"));
    }

    [Fact]
    public void HasUpdate_ServerMaxGreaterThanLocal_ReturnsTrue()
    {
        var assets = new[]
        {
            AssetWithType("v1", "1.5.0", appType: (int)AppType.Client),
            AssetWithType("v2", "2.0.0", appType: (int)AppType.Client),
        };
        Assert.True(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "1.0.0"));
    }

    [Fact]
    public void HasUpdate_ServerMaxEqualToLocal_ReturnsFalse()
    {
        var assets = new[]
        {
            AssetWithType("v1", "1.0.0", appType: (int)AppType.Client),
            AssetWithType("v2", "2.0.0", appType: (int)AppType.Client),
        };
        // max is 2.0.0, local is 2.0.0 → not greater
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "2.0.0"));
    }

    [Fact]
    public void HasUpdate_ServerMaxLessThanLocal_ReturnsFalse()
    {
        var assets = new[]
        {
            AssetWithType("v1", "1.0.0", appType: (int)AppType.Client),
        };
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "3.0.0"));
    }

    [Fact]
    public void HasUpdate_NoAssetsForAppType_ReturnsFalse()
    {
        var assets = new[]
        {
            AssetWithType("v1", "2.0.0", appType: (int)AppType.Client),
        };
        // No Upgrade assets at all
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Upgrade, "1.0.0"));
    }

    [Fact]
    public void HasUpdate_AllAssetsFrozen_ReturnsFalse()
    {
        var assets = new[]
        {
            AssetWithType("frozen", "2.0.0", appType: (int)AppType.Client, isFreeze: true),
        };
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "1.0.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    public void HasUpdate_UnreadableLocalVersion_ReturnsTrue(string? localVersion)
    {
        // When local version can't be determined, err on the side of updating.
        var assets = new[]
        {
            AssetWithType("v1", "2.0.0", appType: (int)AppType.Client),
        };
        Assert.True(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, localVersion));
    }

    [Fact]
    public void HasUpdate_UnreadableLocalVersion_NoServerAssets_ReturnsFalse()
    {
        // Can't read local version AND server has no matching assets → no update
        var assets = new[]
        {
            AssetWithType("v1", "2.0.0", appType: (int)AppType.Upgrade),
        };
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, null));
    }

    [Fact]
    public void HasUpdate_ClientAndUpgradeIndependent()
    {
        var assets = new[]
        {
            AssetWithType("client", "2.0.0", appType: (int)AppType.Client),
            AssetWithType("upgrade", "1.5.0", appType: (int)AppType.Upgrade),
        };
        // Client: 2.0.0 > 1.0.0 → true
        Assert.True(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "1.0.0"));
        // Upgrade: 1.5.0 > 2.0.0 → false (local upgrade is newer)
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Upgrade, "2.0.0"));
    }

    [Fact]
    public void HasUpdate_NullAppType_TreatedAsClient()
    {
        var assets = new[]
        {
            AssetWithType("v1", "2.0.0", appType: null), // null → treated as Client
        };
        Assert.True(DownloadPlanBuilder.HasUpdate(assets, AppType.Client, "1.0.0"));
        // Null AppType is not treated as Upgrade
        Assert.False(DownloadPlanBuilder.HasUpdate(assets, AppType.Upgrade, "1.0.0"));
    }

    // ════════════════════════════════════════════════════════════
    // Build — AppType-aware dual-version overload
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Build_DualVersion_ClientNewer_OnlyClientIncluded()
    {
        var assets = new[]
        {
            AssetWithType("c", "2.0.0", appType: (int)AppType.Client),
            AssetWithType("u", "2.0.0", appType: (int)AppType.Upgrade),
        };
        // ClientVersion=1.0.0, UpgradeClientVersion=2.0.0 → only Client asset passes
        var result = DownloadPlanBuilder.Build(assets, "1.0.0", "2.0.0");
        Assert.True(result.HasAssets);
        Assert.Single(result.Assets);
        Assert.Equal((int)AppType.Client, result.Assets[0].AppType);
    }

    [Fact]
    public void Build_DualVersion_BothNewer_BothIncluded()
    {
        var assets = new[]
        {
            AssetWithType("c", "2.0.0", appType: (int)AppType.Client),
            AssetWithType("u", "1.5.0", appType: (int)AppType.Upgrade),
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0", "1.0.0");
        Assert.Equal(2, result.Assets.Count);
    }

    [Fact]
    public void Build_DualVersion_BothSameAsLocal_ReturnsEmpty()
    {
        var assets = new[]
        {
            AssetWithType("c", "1.0.0", appType: (int)AppType.Client),
            AssetWithType("u", "1.0.0", appType: (int)AppType.Upgrade),
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0", "1.0.0");
        Assert.False(result.HasAssets);
    }

    [Fact]
    public void Build_DualVersion_NullUpgradeClient_FallsBackToClient()
    {
        var assets = new[]
        {
            AssetWithType("u", "1.5.0", appType: (int)AppType.Upgrade),
        };
        // upgradeClientVersion=null → falls back to clientVersion=1.0.0
        // 1.5.0 > 1.0.0 → passes
        var result = DownloadPlanBuilder.Build(assets, "1.0.0", null);
        Assert.True(result.HasAssets);
        Assert.Equal("1.5.0", result.Assets[0].Version);
    }

    [Fact]
    public void Build_DualVersion_MinClientVersionFiltered()
    {
        var assets = new[]
        {
            AssetWithType("ok", "2.0.0", appType: (int)AppType.Client, minClientVersion: "1.0.0"),
            AssetWithType("too-high", "3.0.0", appType: (int)AppType.Client, minClientVersion: "3.0.0"),
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0", "1.0.0");
        Assert.Single(result.Assets);
        Assert.Equal("2.0.0", result.Assets[0].Version);
    }

    [Fact]
    public void Build_SingleVersionOverload_BackwardCompat()
    {
        var assets = new[]
        {
            Asset("a", "2.0.0"),
            Asset("b", "1.0.0"),
        };
        var result = DownloadPlanBuilder.Build(assets, "1.0.0");
        Assert.Single(result.Assets);
        Assert.Equal("2.0.0", result.Assets[0].Version);
    }
}
