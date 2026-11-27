using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Sources;
using System.Text.Json;

namespace CoreTest.Download;

public class PushDownloadSourceTests
{
    [Theory]
    [InlineData(1, 1, 123)]
    [InlineData(2, 2, 456)]
    public async Task MapsFlatServerPacketAndBuildsPlan(int appType, int packageType, int recordId)
    {
        var json = $$"""
            {"Name":"pkg","Url":"https://example.test/pkg.zip","Version":"2.0.0",
             "Hash":"abc","AppType":{{appType}},"RecordId":{{recordId}},"PackageType":{{packageType}},
             "Size":42,"IsForcibly":true,"IsFreeze":false,"ProductId":"p"}
            """;
        var result = await new PushDownloadSource(json).ListAsync();
        var asset = Assert.Single(result.Assets);
        Assert.Equal(recordId, asset.RecordId);
        Assert.Equal(appType, asset.AppType);
        Assert.Equal(packageType, asset.PackageType);
        Assert.Equal(42, asset.Size);
        Assert.Equal("abc", asset.SHA256);
        Assert.True(asset.IsForcibly);
        Assert.Equal(appType == 1, result.HasMainUpdate);
        Assert.Equal(appType == 2, result.HasUpgradeUpdate);
        Assert.Single(DownloadPlanBuilder.Build(result.Assets, "1.0.0", "1.0.0").Assets);
    }

    [Fact]
    public async Task OldPacketWithoutRecordIdCanStillBeDownloaded()
    {
        var source = new PushDownloadSource("""{"Url":"https://example.test/pkg","Version":"2.0.0","AppType":1}""");
        var asset = Assert.Single((await source.ListAsync()).Assets);
        Assert.Equal(0, asset.RecordId);
        Assert.Equal(1, asset.AppType);
    }

    [Fact]
    public async Task ZipSuffixIsNormalizedForDownloadAndApply()
    {
        var source = new PushDownloadSource("""{"Name":"pkg.ZIP","Url":"https://example.test/pkg","Version":"2.0.0","AppType":1}""");
        Assert.Equal("pkg", Assert.Single((await source.ListAsync()).Assets).Name);
    }

    [Fact]
    public void FullOnlyPlansSelectLatestPackageForEachAppType()
    {
        var assets = new[]
        {
            new GeneralUpdate.Core.Download.Models.DownloadAsset("c1", "c1", 1, null, "2.0.0", PackageType: 2, AppType: 1),
            new GeneralUpdate.Core.Download.Models.DownloadAsset("c2", "c2", 1, null, "3.0.0", PackageType: 2, AppType: 1),
            new GeneralUpdate.Core.Download.Models.DownloadAsset("u", "u", 1, null, "2.0.0", PackageType: 2, AppType: 2)
        };
        var plan = DownloadPlanBuilder.Build(assets, "1.0.0", "1.0.0");
        Assert.Equal(2, plan.Assets.Count);
        Assert.Contains(plan.Assets, a => a.Name == "c2");
        Assert.Contains(plan.Assets, a => a.Name == "u");
    }

    [Theory]
    [InlineData("""{"Url":"x","Version":"2.0.0"}""")]
    [InlineData("""{"Url":"x","Version":"2.0.0","AppType":3}""")]
    [InlineData("""{"Version":"2.0.0","AppType":1}""")]
    [InlineData("""{"Url":"x","AppType":1}""")]
    [InlineData("""{"Url":"x","Version":"2.0.0","AppType":1,"RecordId":-1}""")]
    [InlineData("""{"Url":"x","Version":"2.0.0","AppType":1,"PackageType":3}""")]
    [InlineData("""{"Name":"../pkg","Url":"x","Version":"2.0.0","AppType":1}""")]
    [InlineData("""{"Name":"..\\pkg","Url":"x","Version":"2.0.0","AppType":1}""")]
    [InlineData("""{"Name":".zip","Url":"x","Version":"2.0.0","AppType":1}""")]
    [InlineData("null")]
    public void InvalidPacketsAreRejected(string json) =>
        Assert.Throws<JsonException>(() => new PushDownloadSource(json));

    [Fact]
    public async Task CancellationIsHonoured()
    {
        var source = new PushDownloadSource("""{"Url":"x","Version":"2.0.0","AppType":1}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ListAsync(cts.Token));
    }
}
