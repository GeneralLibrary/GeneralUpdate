using System.Text.Json;
using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// Unit tests for configuration model/DTO classes following AAAT (Arrange-Act-Assert-TearDown).
/// Covers: VersionEntry, OssVersionRecord, BaseResponseDTO, VersionRespDTO.
/// </summary>
public class VersionModelTests
{
    #region VersionEntry — property defaults and JSON serialization

    [Fact]
    public void VersionEntry_DefaultInstance_AllNullablePropertiesAreNull()
    {
        // Arrange & Act
        var vi = new VersionEntry();

        // Assert
        Assert.Equal(0, vi.RecordId);
        Assert.Null(vi.Name);
        Assert.Null(vi.Hash);
        Assert.Null(vi.ReleaseDate);
        Assert.Null(vi.Url);
        Assert.Null(vi.Version);
        Assert.Null(vi.AppType);
        Assert.Null(vi.Platform);
        Assert.Null(vi.ProductId);
        Assert.Null(vi.IsForcibly);
        Assert.Null(vi.Format);
        Assert.Null(vi.Size);
        Assert.Null(vi.AuthScheme);
        Assert.Null(vi.AuthToken);
        Assert.Null(vi.UpdateLog);
    }

    [Fact]
    public void VersionEntry_AllProperties_SetCorrectly()
    {
        // Arrange
        var releaseDate = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var vi = new VersionEntry
        {
            RecordId = 42,
            Name = "update-package-v2.0.0",
            Hash = "sha256:abcdef1234567890",
            ReleaseDate = releaseDate,
            Url = "https://cdn.example.com/packages/v2.0.0.zip",
            Version = "2.0.0",
            AppType = 1,
            Platform = 2,
            ProductId = "prod-001",
            IsForcibly = true,
            Format = ".zip",
            Size = 104857600,
            AuthScheme = "Bearer",
            AuthToken = "token-xyz",
            UpdateLog = "Bug fixes and performance improvements"
        };

        // Assert
        Assert.Equal(42, vi.RecordId);
        Assert.Equal("update-package-v2.0.0", vi.Name);
        Assert.Equal("sha256:abcdef1234567890", vi.Hash);
        Assert.Equal(releaseDate, vi.ReleaseDate);
        Assert.Equal("https://cdn.example.com/packages/v2.0.0.zip", vi.Url);
        Assert.Equal("2.0.0", vi.Version);
        Assert.Equal(1, vi.AppType);
        Assert.Equal(2, vi.Platform);
        Assert.Equal("prod-001", vi.ProductId);
        Assert.True(vi.IsForcibly);
        Assert.Equal(".zip", vi.Format);
        Assert.Equal(104857600, vi.Size);
        Assert.Equal("Bearer", vi.AuthScheme);
        Assert.Equal("token-xyz", vi.AuthToken);
        Assert.Equal("Bug fixes and performance improvements", vi.UpdateLog);
    }

    [Fact]
    public void VersionEntry_JsonRoundTrip_AllProperties()
    {
        // Arrange
        var original = new VersionEntry
        {
            RecordId = 7,
            Name = "MyPackage",
            Hash = "abc123",
            ReleaseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Url = "https://example.com/pkg",
            Version = "1.0.0",
            AppType = 1,
            Platform = 0,
            ProductId = "p1",
            IsForcibly = false,
            Format = ".zip",
            Size = 5000000,
            AuthScheme = "Bearer",
            AuthToken = "tok",
            UpdateLog = "v1 release"
        };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VersionEntry>(json, options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.RecordId, deserialized.RecordId);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Hash, deserialized.Hash);
        Assert.Equal(original.Url, deserialized.Url);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.AppType, deserialized.AppType);
        Assert.Equal(original.Platform, deserialized.Platform);
        Assert.Equal(original.ProductId, deserialized.ProductId);
        Assert.Equal(original.IsForcibly, deserialized.IsForcibly);
        Assert.Equal(original.Format, deserialized.Format);
        Assert.Equal(original.Size, deserialized.Size);
        Assert.Equal(original.AuthScheme, deserialized.AuthScheme);
        Assert.Equal(original.AuthToken, deserialized.AuthToken);
        Assert.Equal(original.UpdateLog, deserialized.UpdateLog);
    }

    [Fact]
    public void VersionEntry_JsonSerialization_UsesCorrectJsonPropertyNames()
    {
        // Arrange
        var vi = new VersionEntry
        {
            RecordId = 1,
            Name = "test",
            Hash = "h",
            ReleaseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Url = "http://x",
            Version = "v1",
            AppType = 1,
            Platform = 2,
            ProductId = "p",
            IsForcibly = true,
            Format = ".zip",
            Size = 100,
            AuthScheme = "Basic",
            AuthToken = "t",
            UpdateLog = "log"
        };

        // Act
        var json = JsonSerializer.Serialize(vi);

        // Assert — verify JSON property name casing matches [JsonPropertyName] attributes
        Assert.Contains("\"recordId\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"hash\"", json);
        Assert.Contains("\"releaseDate\"", json);
        Assert.Contains("\"url\"", json);
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"appType\"", json);
        Assert.Contains("\"platform\"", json);
        Assert.Contains("\"productId\"", json);
        Assert.Contains("\"isForcibly\"", json);
        Assert.Contains("\"format\"", json);
        Assert.Contains("\"size\"", json);
        Assert.Contains("\"authScheme\"", json);
        Assert.Contains("\"authToken\"", json);
        Assert.Contains("\"updateLog\"", json);
    }

    #endregion

    #region OssVersionRecord — property defaults

    [Fact]
    public void OssVersionRecord_DefaultInstance_HasDefaultDate()
    {
        // Arrange & Act
        var voss = new OssVersionRecord();

        // Assert
        Assert.Equal(default(DateTime), voss.PubTime);
        Assert.Null(voss.PacketName);
        Assert.Null(voss.Hash);
        Assert.Null(voss.Version);
        Assert.Null(voss.Url);
    }

    [Fact]
    public void OssVersionRecord_AllProperties_SetCorrectly()
    {
        // Arrange
        var pubTime = new DateTime(2025, 3, 1);

        // Act
        var voss = new OssVersionRecord
        {
            PubTime = pubTime,
            PacketName = "update.zip",
            Hash = "sha256:xyz",
            Version = "2.0.0",
            Url = "https://cdn.example.com/update.zip"
        };

        // Assert
        Assert.Equal(pubTime, voss.PubTime);
        Assert.Equal("update.zip", voss.PacketName);
        Assert.Equal("sha256:xyz", voss.Hash);
        Assert.Equal("2.0.0", voss.Version);
        Assert.Equal("https://cdn.example.com/update.zip", voss.Url);
    }

    [Fact]
    public void OssVersionRecord_JsonSerialization_UsesCorrectJsonPropertyNames()
    {
        // Arrange
        var voss = new OssVersionRecord
        {
            PubTime = new DateTime(2025, 1, 1),
            PacketName = "pkg.zip",
            Hash = "abc",
            Version = "1.0",
            Url = "http://x"
        };

        // Act
        var json = JsonSerializer.Serialize(voss);

        // Assert
        Assert.Contains("\"PubTime\"", json);
        Assert.Contains("\"PacketName\"", json);
        Assert.Contains("\"Hash\"", json);
        Assert.Contains("\"Version\"", json);
        Assert.Contains("\"Url\"", json);
    }

    [Fact]
    public void OssVersionRecord_JsonRoundTrip_PreservesAllValues()
    {
        // Arrange
        var pubTime = new DateTime(2025, 6, 1, 12, 0, 0);
        var original = new OssVersionRecord
        {
            PubTime = pubTime,
            PacketName = "package-v3.zip",
            Hash = "sha256:deadbeef",
            Version = "3.0.0",
            Url = "https://cdn.example.com/v3.zip"
        };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OssVersionRecord>(json, options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.PubTime, deserialized.PubTime);
        Assert.Equal(original.PacketName, deserialized.PacketName);
        Assert.Equal(original.Hash, deserialized.Hash);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.Url, deserialized.Url);
    }

    #endregion

    #region BaseResponseDTO<TBody> — generic wrapper

    [Fact]
    public void BaseResponseDTO_IntBody_WrapsCorrectly()
    {
        // Arrange
        var dto = new BaseResponseDTO<int>
        {
            Code = 200,
            Body = 42,
            Message = "OK"
        };

        // Assert
        Assert.Equal(200, dto.Code);
        Assert.Equal(42, dto.Body);
        Assert.Equal("OK", dto.Message);
    }

    [Fact]
    public void BaseResponseDTO_StringBody_WrapsCorrectly()
    {
        // Arrange
        var dto = new BaseResponseDTO<string>
        {
            Code = 500,
            Body = "Internal Server Error",
            Message = "Something went wrong"
        };

        // Assert
        Assert.Equal(500, dto.Code);
        Assert.Equal("Internal Server Error", dto.Body);
        Assert.Equal("Something went wrong", dto.Message);
    }

    [Fact]
    public void BaseResponseDTO_VersionEntryList_WrapsCorrectly()
    {
        // Arrange
        var versions = new List<VersionEntry>
        {
            new() { Version = "1.0.0", Hash = "abc" },
            new() { Version = "2.0.0", Hash = "def" }
        };
        var dto = new BaseResponseDTO<List<VersionEntry>>
        {
            Code = 200,
            Body = versions,
            Message = "Success"
        };

        // Assert
        Assert.Equal(200, dto.Code);
        Assert.Equal(2, dto.Body.Count);
        Assert.Equal("1.0.0", dto.Body[0].Version);
        Assert.Equal("2.0.0", dto.Body[1].Version);
    }

    [Fact]
    public void BaseResponseDTO_JsonSerialization_UsesCamelCasePropertyNames()
    {
        // Arrange
        var dto = new BaseResponseDTO<string>
        {
            Code = 200,
            Body = "test-body",
            Message = "success"
        };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert — property names use camelCase from [JsonPropertyName]
        Assert.Contains("\"code\"", json);
        Assert.Contains("\"body\"", json);
        Assert.Contains("\"message\"", json);
    }

    [Fact]
    public void BaseResponseDTO_DefaultInstance_CodeZero()
    {
        // Arrange & Act
        var dto = new BaseResponseDTO<string>();

        // Assert
        Assert.Equal(0, dto.Code);
        Assert.Null(dto.Body);
        Assert.Null(dto.Message);
    }

    #endregion

    #region VersionRespDTO — typed alias

    [Fact]
    public void VersionRespDTO_IsAssignableToBaseResponse()
    {
        // Arrange
        var dto = new VersionRespDTO
        {
            Code = 200,
            Body = new List<VersionEntry> { new() { Version = "1.0.0" } },
            Message = "OK"
        };

        // Assert
        Assert.IsAssignableFrom<BaseResponseDTO<List<VersionEntry>>>(dto);
        Assert.Equal(200, dto.Code);
        Assert.Single(dto.Body);
        Assert.Equal("1.0.0", dto.Body[0].Version);
    }

    #endregion
}
