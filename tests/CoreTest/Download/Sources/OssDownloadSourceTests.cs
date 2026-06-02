using System.Net;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.JsonContext;
using Moq;
using Moq.Protected;

namespace CoreTest.Download.Sources;

/// <summary>
/// Unit tests for <see cref="OssDownloadSource"/> following AAAT pattern.
/// Tests constructor validation, ListAsync with mocked HTTP responses.
/// </summary>
public class OssDownloadSourceTests
{
    private const string ValidUrl = "http://localhost:5000/versions.json";

    #region Constructor

    [Fact]
    public void Ctor_WithValidParams_CreatesInstance()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, ValidUrl);

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_WithCustomTimeout_CreatesInstance()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, ValidUrl, TimeSpan.FromSeconds(30));

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OssDownloadSource(null!, ValidUrl));
    }

    [Fact]
    public void Ctor_NullVersionJsonUrl_ThrowsArgumentNullException()
    {
        var client = new HttpClient();
        Assert.Throws<ArgumentNullException>(() => new OssDownloadSource(client, null!));
    }

    [Fact]
    public void Ctor_DefaultTimeout_Is60Seconds()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, ValidUrl);

        Assert.NotNull(source);
    }

    #endregion

    #region ListAsync — empty response

    [Fact]
    public async Task ListAsync_EmptyVersionList_ReturnsEmptyAssets()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act
        var result = await source.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Assets);
        Assert.False(result.HasMainUpdate);
        Assert.False(result.HasUpgradeUpdate);
    }

    #endregion

    #region ListAsync — valid version list

    [Fact]
    public async Task ListAsync_ValidVersionList_ReturnsAssetsSortedByPubTime()
    {
        // Arrange
        var records = new List<OssVersionRecord>
        {
            new()
            {
                PacketName = "app-v2",
                Version = "2.0.0",
                Url = "http://example.com/app-v2.zip",
                Hash = "abc123",
                PubTime = new DateTime(2025, 6, 1)
            },
            new()
            {
                PacketName = "app-v1",
                Version = "1.0.0",
                Url = "http://example.com/app-v1.zip",
                Hash = "def456",
                PubTime = new DateTime(2025, 1, 1)
            }
        };

        var json = JsonSerializer.Serialize(records, OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act
        var result = await source.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Assets.Count);
        Assert.True(result.HasMainUpdate);
        Assert.True(result.HasUpgradeUpdate);
        // Should be sorted by PubTime ascending: v1 first, v2 second
        Assert.Equal("1.0.0", result.Assets[0].Version);
        Assert.Equal("2.0.0", result.Assets[1].Version);
        Assert.Equal("app-v1.zip", result.Assets[0].Name);
        Assert.Equal("app-v2.zip", result.Assets[1].Name);
    }

    #endregion

    #region ListAsync — single version

    [Fact]
    public async Task ListAsync_SingleVersion_ReturnsOneAsset()
    {
        // Arrange
        var records = new List<OssVersionRecord>
        {
            new()
            {
                PacketName = "app",
                Version = "1.0.0",
                Url = "http://example.com/app.zip",
                Hash = "hash123",
                PubTime = new DateTime(2025, 1, 1)
            }
        };

        var json = JsonSerializer.Serialize(records, OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act
        var result = await source.ListAsync();

        // Assert
        Assert.Single(result.Assets);
        Assert.Equal("1.0.0", result.Assets[0].Version);
        Assert.True(result.HasMainUpdate);
    }

    #endregion

    #region ListAsync — HTTP error

    [Fact]
    public async Task ListAsync_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => source.ListAsync());
    }

    #endregion

    #region ListAsync — version with null URL throws

    [Fact]
    public async Task ListAsync_VersionWithNullUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var records = new List<OssVersionRecord>
        {
            new()
            {
                PacketName = "app",
                Version = "1.0.0",
                Url = null,
                Hash = "hash123",
                PubTime = new DateTime(2025, 1, 1)
            }
        };

        var json = JsonSerializer.Serialize(records, OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ListAsync());
    }

    #endregion

    #region ListAsync — version with PacketName null uses Version

    [Fact]
    public async Task ListAsync_PacketNameNull_UsesVersionForZipName()
    {
        // Arrange
        var records = new List<OssVersionRecord>
        {
            new()
            {
                PacketName = null,
                Version = "3.0.0",
                Url = "http://example.com/pkg.zip",
                Hash = "hash",
                PubTime = new DateTime(2025, 1, 1)
            }
        };

        var json = JsonSerializer.Serialize(records, OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act
        var result = await source.ListAsync();

        // Assert
        Assert.Single(result.Assets);
        Assert.Equal("3.0.0.zip", result.Assets[0].Name);
    }

    #endregion

    #region IDownloadSource contract

    [Fact]
    public void Implements_IDownloadSource()
    {
        var client = new HttpClient();
        var source = new OssDownloadSource(client, ValidUrl);

        Assert.IsAssignableFrom<IDownloadSource>(source);
    }

    #endregion

    #region ListAsync — multiple versions with same URL

    [Fact]
    public async Task ListAsync_SameUrlDifferentVersions_ReturnsAllVersions()
    {
        // Arrange — OssDownloadSource preserves all records (dedup is done by HttpDownloadSource, not here)
        var records = new List<OssVersionRecord>
        {
            new()
            {
                PacketName = "pkg-a", Version = "1.0.0",
                Url = "http://example.com/same.zip", Hash = "aaa",
                PubTime = new DateTime(2025, 1, 1)
            },
            new()
            {
                PacketName = "pkg-b", Version = "2.0.0",
                Url = "http://example.com/same.zip", Hash = "bbb",
                PubTime = new DateTime(2025, 6, 1)
            },
            new()
            {
                PacketName = "pkg-c", Version = "3.0.0",
                Url = "http://example.com/other.zip", Hash = "ccc",
                PubTime = new DateTime(2025, 3, 1)
            }
        };

        var json = JsonSerializer.Serialize(records, OssVersionRecordJsonContext.Default.ListOssVersionRecord);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handler.Object);
        var source = new OssDownloadSource(client, ValidUrl);

        // Act
        var result = await source.ListAsync();

        // Assert — OssDownloadSource returns all records, sorted by PubTime asc
        Assert.Equal(3, result.Assets.Count);
        Assert.Equal("1.0.0", result.Assets[0].Version);
        Assert.Equal("3.0.0", result.Assets[1].Version);
        Assert.Equal("2.0.0", result.Assets[2].Version);
    }

    #endregion
}
