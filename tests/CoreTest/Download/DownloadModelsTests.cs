using GeneralUpdate.Core.Download.Models;

namespace CoreTest.Download;

/// <summary>
/// AAAT unit tests for <see cref="DownloadAsset"/>, <see cref="DownloadPlan"/>,
/// <see cref="DownloadProgress"/>, <see cref="DownloadResult"/> record types.
/// Covers: default construction, value equality, Empty plan, HasAssets, all statuses/priorities.
/// </summary>
public class DownloadModelsTests
{
    #region DownloadAsset

    [Fact]
    public void DownloadAsset_Defaults_AreSensible()
    {
        var asset = new DownloadAsset("test", "http://url", 100, null, "1.0.0");

        Assert.Equal("test", asset.Name);
        Assert.Equal("http://url", asset.Url);
        Assert.Equal(100, asset.Size);
        Assert.Null(asset.SHA256);
        Assert.Equal("1.0.0", asset.Version);
        Assert.Equal(DownloadPriority.Normal, asset.Priority);
        Assert.Equal(0, asset.PackageType);
        Assert.Null(asset.MinClientVersion);
        Assert.False(asset.IsForcibly);
        Assert.False(asset.IsFreeze);
    }

    [Fact]
    public void DownloadAsset_FullySpecified_AllPropertiesSet()
    {
        var asset = new DownloadAsset(
            "package.zip", "https://cdn/pkg.zip", 1024, "abc123", "3.0.0",
            DownloadPriority.High, 0,
            MinClientVersion: "2.0.0",
            FallbackFullName: "full-pkg",
            FallbackFullUrl: "https://cdn/full.zip",
            FallbackFullHash: "fullhash",
            IsForcibly: true, IsFreeze: false
        );

        Assert.Equal("package.zip", asset.Name);
        Assert.Equal("https://cdn/pkg.zip", asset.Url);
        Assert.Equal(1024, asset.Size);
        Assert.Equal("abc123", asset.SHA256);
        Assert.Equal("3.0.0", asset.Version);
        Assert.Equal(DownloadPriority.High, asset.Priority);
        Assert.Equal("2.0.0", asset.MinClientVersion);
        Assert.Equal("full-pkg", asset.FallbackFullName);
        Assert.Equal("https://cdn/full.zip", asset.FallbackFullUrl);
        Assert.Equal("fullhash", asset.FallbackFullHash);
        Assert.True(asset.IsForcibly);
        Assert.False(asset.IsFreeze);
    }

    [Fact]
    public void DownloadAsset_ValueEquality_SamePropsEqual()
    {
        var a = new DownloadAsset("a", "url", 100, "hash", "1.0", IsForcibly: true);
        var b = new DownloadAsset("a", "url", 100, "hash", "1.0", IsForcibly: true);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void DownloadAsset_ValueEquality_DifferentForciblyNotEqual()
    {
        var a = new DownloadAsset("a", "url", 100, "hash", "1.0", IsForcibly: true);
        var b = new DownloadAsset("a", "url", 100, "hash", "1.0", IsForcibly: false);

        Assert.NotEqual(a, b);
    }

    #endregion

    #region DownloadPlan

    [Fact]
    public void DownloadPlan_Empty_HasNoAssets()
    {
        var plan = DownloadPlan.Empty;
        Assert.False(plan.HasAssets);
        Assert.Empty(plan.Assets);
        Assert.False(plan.IsForcibly);
    }

    [Fact]
    public void DownloadPlan_Empty_IsSingleton()
    {
        var a = DownloadPlan.Empty;
        var b = DownloadPlan.Empty;
        Assert.Same(a, b);
    }

    [Fact]
    public void DownloadPlan_WithAssets_HasAssetsTrue()
    {
        var assets = new List<DownloadAsset> { new("a", "u", 100, null, "2.0") };
        var plan = new DownloadPlan(assets, false);

        Assert.True(plan.HasAssets);
        Assert.Single(plan.Assets);
    }

    [Fact]
    public void DownloadPlan_Empty_IsForciblyFalse()
    {
        var plan = DownloadPlan.Empty;
        Assert.False(plan.IsForcibly);
    }

    [Fact]
    public void DownloadPlan_ForciblyTrue_Stored()
    {
        var plan = new DownloadPlan(new List<DownloadAsset>(), true);
        Assert.True(plan.IsForcibly);
    }

    #endregion

    #region DownloadProgress

    [Fact]
    public void DownloadProgress_AllPropertiesAssigned()
    {
        var dp = new DownloadProgress("asset.zip", 512, 1024, 50.0, DownloadStatus.Downloading);

        Assert.Equal("asset.zip", dp.AssetName);
        Assert.Equal(512, dp.BytesDownloaded);
        Assert.Equal(1024, dp.TotalBytes);
        Assert.Equal(50.0, dp.Percentage);
        Assert.Equal(DownloadStatus.Downloading, dp.Status);
    }

    [Fact]
    public void DownloadProgress_NullAssetName_Works()
    {
        var dp = new DownloadProgress(null, 100, 200, 50.0, DownloadStatus.Pending);
        Assert.Null(dp.AssetName);
    }

    [Fact]
    public void DownloadProgress_NullTotalBytes_Works()
    {
        var dp = new DownloadProgress("a.zip", 500, null, 0, DownloadStatus.Downloading);
        Assert.Null(dp.TotalBytes);
    }

    [Fact]
    public void DownloadProgress_AllStatuses_Supported()
    {
        foreach (DownloadStatus status in Enum.GetValues<DownloadStatus>())
        {
            var dp = new DownloadProgress("a", 0, 0, 0, status);
            Assert.Equal(status, dp.Status);
        }
    }

    #endregion

    #region DownloadResult

    [Fact]
    public void DownloadResult_Success_AllFieldsSet()
    {
        var asset = new DownloadAsset("pkg.zip", "http://u", 500, "hash", "2.0");
        var result = new DownloadResult(asset, "/local/pkg.zip", 500,
            TimeSpan.FromSeconds(5), 1, true, null);

        Assert.Same(asset, result.Asset);
        Assert.Equal("/local/pkg.zip", result.LocalPath);
        Assert.Equal(500, result.DownloadedBytes);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
        Assert.Equal(1, result.RetryCount);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void DownloadResult_Failure_HasErrorMessage()
    {
        var asset = new DownloadAsset("fail.zip", "http://u", 100, null, "1.0");
        var result = new DownloadResult(asset, "", 0, TimeSpan.Zero, 3, false, "Network timeout");

        Assert.False(result.Success);
        Assert.Equal(3, result.RetryCount);
        Assert.Equal("Network timeout", result.ErrorMessage);
    }

    #endregion

    #region DownloadPriority

    [Fact]
    public void DownloadPriority_Ordering_LowLessThanNormal()
    {
        Assert.True(DownloadPriority.Low < DownloadPriority.Normal);
        Assert.True(DownloadPriority.Normal < DownloadPriority.High);
    }

    #endregion
}
