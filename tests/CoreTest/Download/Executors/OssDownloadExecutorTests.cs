using System.Net;
using System.Text;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Models;
using Moq;
using Moq.Protected;

namespace CoreTest.Download.Executors;

/// <summary>
/// Unit tests for <see cref="OssDownloadExecutor"/> following AAAT pattern.
/// Tests constructor validation, ExecuteAsync with mocked HTTP responses.
/// </summary>
public class OssDownloadExecutorTests
{
    private static DownloadAsset CreateAsset(string url = "http://example.com/pkg.zip") => new(
        Name: "pkg.zip",
        Url: url,
        Size: 1024,
        SHA256: "abc123",
        Version: "1.0.0");

    #region Constructor

    [Fact]
    public void Ctor_WithValidHttpClient_CreatesInstance()
    {
        var client = new HttpClient();
        var executor = new OssDownloadExecutor(client);
        Assert.NotNull(executor);
    }

    [Fact]
    public void Ctor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OssDownloadExecutor(null!));
    }

    #endregion

    #region ExecuteAsync — successful download

    [Fact]
    public async Task ExecuteAsync_SuccessfulDownload_ReturnsCompletedResult()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("test-payload-content");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(payload)
                {
                    Headers = { ContentLength = payload.Length }
                }
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();
        var destPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.zip");

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(destPath, result.LocalPath);
            Assert.True(File.Exists(destPath));
            Assert.Equal(payload.Length, result.DownloadedBytes);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    #endregion

    #region ExecuteAsync — HTTP error

    [Fact]
    public async Task ExecuteAsync_NonSuccessStatusCode_ReturnsFailedResult()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();
        var destPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.zip");

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    #endregion

    #region ExecuteAsync — progress reporting

    [Fact]
    public async Task ExecuteAsync_WithProgress_Reports100Percent()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("test-content");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(payload)
                {
                    Headers = { ContentLength = payload.Length }
                }
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();
        var destPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.zip");

        var progressValues = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressValues.Add(p));

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath, progress);

            // Assert
            Assert.True(result.Success);
            Assert.Contains(progressValues, p => p.Percentage == 100);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    #endregion

    #region ExecuteAsync — directory creation

    [Fact]
    public async Task ExecuteAsync_CreatesDestinationDirectory()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("content");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(payload)
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();

        var subDir = Path.Combine(Path.GetTempPath(), $"oss_test_{Guid.NewGuid()}");
        var destPath = Path.Combine(subDir, "nested", "file.zip");

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath);

            // Assert
            Assert.True(result.Success);
            Assert.True(Directory.Exists(Path.GetDirectoryName(destPath)));
            Assert.True(File.Exists(destPath));
        }
        finally
        {
            if (Directory.Exists(subDir))
                Directory.Delete(subDir, recursive: true);
        }
    }

    #endregion

    #region ExecuteAsync — execution time recorded

    [Fact]
    public async Task ExecuteAsync_RecordsDuration()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("data");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(payload)
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();
        var destPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.zip");

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath);

            // Assert
            Assert.True(result.Duration > TimeSpan.Zero);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    #endregion

    #region ExecuteAsync — null progress does not throw

    [Fact]
    public async Task ExecuteAsync_NullProgress_DoesNotThrow()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("data");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(payload)
            });

        var client = new HttpClient(handler.Object);
        var executor = new OssDownloadExecutor(client);
        var asset = CreateAsset();
        var destPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.zip");

        try
        {
            // Act
            var result = await executor.ExecuteAsync(asset, destPath, null);

            // Assert
            Assert.True(result.Success);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    #endregion
}
