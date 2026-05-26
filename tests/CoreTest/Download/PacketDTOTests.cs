namespace CoreTest.Download;

using GeneralUpdate.Core.Download.Abstractions;

/// <summary>
/// AAAT unit tests for <see cref="PacketDTO"/> and related DTO records.
/// Covers: default values, full assignment, nullable properties, VersionRequest, VersionResponse.
/// </summary>
public class PacketDTOTests
{
    #region PacketDTO

    [Fact]
    public void PacketDTO_Default_AllNullablePropsAreNull()
    {
        var dto = new PacketDTO();

        Assert.Null(dto.Name);
        Assert.Null(dto.Hash);
        Assert.Null(dto.ReleaseDate);
        Assert.Null(dto.Url);
        Assert.Null(dto.Version);
        Assert.Null(dto.AppType);
        Assert.Null(dto.Platform);
        Assert.Null(dto.ProductId);
        Assert.Null(dto.IsForcibly);
        Assert.Null(dto.IsFreeze);
        Assert.Null(dto.Format);
        Assert.Null(dto.Size);
        Assert.Null(dto.FromVersion);
        Assert.Null(dto.IsCrossVersion);
        Assert.Null(dto.MinClientVersion);
        Assert.Null(dto.SourceArchiveHash);
        Assert.Null(dto.TargetArchiveHash);
    }

    [Fact]
    public void PacketDTO_FullAssignment_AllPropsSet()
    {
        var dto = new PacketDTO
        {
            Name = "UpdatePack",
            Hash = "hash123",
            ReleaseDate = new DateTime(2025, 3, 15),
            Url = "https://cdn.example.com/pack.zip",
            Version = "2.0.0",
            AppType = 1,
            Platform = 0,
            ProductId = "prod-1",
            IsForcibly = true,
            IsFreeze = false,
            Format = ".zip",
            Size = 2048,
            FromVersion = "1.0.0",
            IsCrossVersion = true,
            MinClientVersion = "1.5.0",
            SourceArchiveHash = "srcHash",
            TargetArchiveHash = "tgtHash"
        };

        Assert.Equal("UpdatePack", dto.Name);
        Assert.Equal("hash123", dto.Hash);
        Assert.Equal(new DateTime(2025, 3, 15), dto.ReleaseDate);
        Assert.Equal(".zip", dto.Format);
        Assert.Equal(2048, dto.Size);
        Assert.Equal("1.0.0", dto.FromVersion);
        Assert.True(dto.IsCrossVersion);
        Assert.Equal("1.5.0", dto.MinClientVersion);
        Assert.Equal("srcHash", dto.SourceArchiveHash);
        Assert.Equal("tgtHash", dto.TargetArchiveHash);
    }

    [Fact]
    public void PacketDTO_IsForcibly_NullableTriState()
    {
        var dto = new PacketDTO();
        Assert.Null(dto.IsForcibly);

        dto.IsForcibly = true;
        Assert.True(dto.IsForcibly);

        dto.IsForcibly = null;
        Assert.Null(dto.IsForcibly);
    }

    [Fact]
    public void PacketDTO_IsFreeze_NullableTriState()
    {
        var dto = new PacketDTO();
        Assert.Null(dto.IsFreeze);

        dto.IsFreeze = false;
        Assert.False(dto.IsFreeze);

        dto.IsFreeze = null;
        Assert.Null(dto.IsFreeze);
    }

    #endregion

    #region VersionRequest

    [Fact]
    public void VersionRequest_AllFieldsAssigned()
    {
        var req = new VersionRequest("MyApp", "1.0.0", "2.0.0", 1, "prod-001");

        Assert.Equal("MyApp", req.AppName);
        Assert.Equal("1.0.0", req.ClientVersion);
        Assert.Equal("2.0.0", req.UpgradeClientVersion);
        Assert.Equal(1, req.Platform);
        Assert.Equal("prod-001", req.ProductId);
    }

    [Fact]
    public void VersionRequest_NullableFields_CanBeNull()
    {
        var req = new VersionRequest("App", "1.0", null, null, null);

        Assert.Null(req.UpgradeClientVersion);
        Assert.Null(req.Platform);
        Assert.Null(req.ProductId);
    }

    #endregion

    #region VersionResponse

    [Fact]
    public void VersionResponse_NoUpdate_EmptyPackets()
    {
        var resp = new VersionResponse(false, null);

        Assert.False(resp.HasUpdate);
        Assert.Null(resp.Packets);
    }

    [Fact]
    public void VersionResponse_HasUpdate_WithPackets()
    {
        var packets = new List<PacketDTO> { new() { Name = "p1" }, new() { Name = "p2" } };
        var resp = new VersionResponse(true, packets);

        Assert.True(resp.HasUpdate);
        Assert.Equal(2, resp.Packets!.Count);
    }

    [Fact]
    public void VersionResponse_HasUpdateTrue_ButNullPackets_Works()
    {
        var resp = new VersionResponse(true, null);

        Assert.True(resp.HasUpdate);
        Assert.Null(resp.Packets);
    }

    #endregion
}
