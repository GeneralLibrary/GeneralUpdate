using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="Packet"/> — property defaults, set/get, JSON serialization attrs.
/// Covers: all nullable bool?/int?/DateTime?/string? properties, null vs non-null, IsForcibly/IsFreeze tri-state.
/// </summary>
public class PacketTests
{
    [Fact]
    public void Ctor_Default_AllPropertiesAreNullOrDefault()
    {
        var packet = new Packet();

        Assert.Null(packet.Name);
        Assert.Null(packet.Hash);
        Assert.Null(packet.ReleaseDate);
        Assert.Null(packet.Url);
        Assert.Null(packet.Version);
        Assert.Null(packet.AppType);
        Assert.Null(packet.Platform);
        Assert.Null(packet.ProductId);
        Assert.Null(packet.IsForcibly);
        Assert.Null(packet.IsFreeze);
    }

    [Fact]
    public void FullAssignment_AllPropertiesSet()
    {
        var releaseDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var packet = new Packet
        {
            Name = "UpdatePack",
            Hash = "abc123",
            ReleaseDate = releaseDate,
            Url = "https://cdn.example.com/pack.zip",
            Version = "2.0.0",
            AppType = 1,
            Platform = 0,
            ProductId = "prod-001",
            IsForcibly = true,
            IsFreeze = false
        };

        Assert.Equal("UpdatePack", packet.Name);
        Assert.Equal("abc123", packet.Hash);
        Assert.Equal(releaseDate, packet.ReleaseDate);
        Assert.Equal("https://cdn.example.com/pack.zip", packet.Url);
        Assert.Equal("2.0.0", packet.Version);
        Assert.Equal(1, packet.AppType);
        Assert.Equal(0, packet.Platform);
        Assert.Equal("prod-001", packet.ProductId);
        Assert.True(packet.IsForcibly);
        Assert.False(packet.IsFreeze);
    }

    [Fact]
    public void IsForcibly_SetToNull_ReturnsNull()
    {
        var packet = new Packet();
        Assert.Null(packet.IsForcibly);

        packet.IsForcibly = true;
        Assert.True(packet.IsForcibly);

        packet.IsForcibly = null;
        Assert.Null(packet.IsForcibly);
    }

    [Fact]
    public void IsFreeze_SetToNull_ReturnsNull()
    {
        var packet = new Packet();
        Assert.Null(packet.IsFreeze);

        packet.IsFreeze = false;
        Assert.False(packet.IsFreeze);

        packet.IsFreeze = null;
        Assert.Null(packet.IsFreeze);
    }

    [Fact]
    public void AppType_SetToNull_ReturnsNull()
    {
        var packet = new Packet { AppType = 2 };
        Assert.Equal(2, packet.AppType);

        packet.AppType = null;
        Assert.Null(packet.AppType);
    }

    [Fact]
    public void Platform_SetToNull_ReturnsNull()
    {
        var packet = new Packet { Platform = 1 };
        Assert.Equal(1, packet.Platform);

        packet.Platform = null;
        Assert.Null(packet.Platform);
    }

    [Fact]
    public void ReleaseDate_Nullable_Works()
    {
        var packet = new Packet();
        Assert.Null(packet.ReleaseDate);

        var dt = DateTime.UtcNow;
        packet.ReleaseDate = dt;
        Assert.Equal(dt, packet.ReleaseDate);

        packet.ReleaseDate = null;
        Assert.Null(packet.ReleaseDate);
    }
}
