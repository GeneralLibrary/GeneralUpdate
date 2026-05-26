using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="VersionOSS"/> — property defaults and set/get.
/// Covers: all properties, DateTime precision, null/empty strings.
/// </summary>
public class VersionOSSTests
{
    [Fact]
    public void Ctor_Default_AllPropertiesAreDefault()
    {
        var version = new VersionOSS();

        Assert.Equal(default(DateTime), version.PubTime);
        Assert.Null(version.PacketName);
        Assert.Null(version.Hash);
        Assert.Null(version.Version);
        Assert.Null(version.Url);
    }

    [Fact]
    public void FullAssignment_AllPropertiesSet()
    {
        var pubTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var version = new VersionOSS
        {
            PubTime = pubTime,
            PacketName = "Release-2.0.0.zip",
            Hash = "def456abc",
            Version = "2.0.0",
            Url = "https://oss.example.com/bucket/release.zip"
        };

        Assert.Equal(pubTime, version.PubTime);
        Assert.Equal("Release-2.0.0.zip", version.PacketName);
        Assert.Equal("def456abc", version.Hash);
        Assert.Equal("2.0.0", version.Version);
        Assert.Equal("https://oss.example.com/bucket/release.zip", version.Url);
    }

    [Fact]
    public void PacketName_SetToNull_Works()
    {
        var version = new VersionOSS { PacketName = "something" };
        Assert.Equal("something", version.PacketName);

        version.PacketName = null;
        Assert.Null(version.PacketName);
    }

    [Fact]
    public void Hash_SetToNull_Works()
    {
        var version = new VersionOSS { Hash = "hash" };
        version.Hash = null;
        Assert.Null(version.Hash);
    }

    [Fact]
    public void Version_SetToNull_Works()
    {
        var version = new VersionOSS { Version = "1.0" };
        version.Version = null;
        Assert.Null(version.Version);
    }

    [Fact]
    public void Url_SetToNull_Works()
    {
        var version = new VersionOSS { Url = "https://a" };
        version.Url = null;
        Assert.Null(version.Url);
    }

    [Fact]
    public void PubTime_UtcNow_StoredCorrectly()
    {
        var now = DateTime.UtcNow;
        var version = new VersionOSS { PubTime = now };

        Assert.Equal(now, version.PubTime);
    }

    [Fact]
    public void PubTime_MinValue_StoredCorrectly()
    {
        var version = new VersionOSS { PubTime = DateTime.MinValue };
        Assert.Equal(DateTime.MinValue, version.PubTime);
    }

    [Fact]
    public void PubTime_MaxValue_StoredCorrectly()
    {
        var version = new VersionOSS { PubTime = DateTime.MaxValue };
        Assert.Equal(DateTime.MaxValue, version.PubTime);
    }
}
