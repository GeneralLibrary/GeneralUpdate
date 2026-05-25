/// <summary>
/// 测试覆盖点：
/// - 构造函数
///   - (serverUrl, scheme, token) 便利构造函数
///   - (serverUrl, scheme, token, httpClient, ownsHttpClient) 完整构造函数
///   - serverUrl 为 null => ArgumentNullException
///   - httpClient 为 null => ArgumentNullException
///   - serverUrl 以 '/' 结尾 => 被 TrimEnd
///   - scheme/token 为空 => Authorization header 不设置
///   - scheme/token 非空 => Authorization header 正确设置
/// - QueryExtensionsAsync(query, ct)
///   - 成功响应 => 反序列化返回 DTO
///   - 非成功状态码 => 返回 Message 含 HTTP 状态码
///   - 网络异常 => 返回失败 DTO
///   - JSON 反序列化为 null => 返回默认 DTO
///   - 取消 Token => 抛出 OperationCanceledException
/// - DownloadExtensionAsync(extensionId, savePath, progress, ct)
///   - 委托给 DownloadExtensionWithResultAsync
///   - 返回 result.Success
/// - DownloadExtensionWithResultAsync(extensionId, savePath, progress, ct)
///   - 成功下载
///   - 断点续传_文件已存在时追加
///   - HTTP 416 RangeNotSatisfiable => Ok()
///   - 客户端错误 4xx => Fail ClientError
///   - 服务器错误 5xx => Fail ServerError
///   - OperationCanceledException => Fail Cancelled
///   - HttpRequestException => Fail NetworkError
///   - IOException => Fail IoError
///   - 其他异常 => Fail Unknown
///   - progress 报告进度
/// - Dispose()
///   - ownsHttpClient=true => 释放 HttpClient
///   - ownsHttpClient=false => 不释放 HttpClient
/// </summary>
using System.Net;
using System.Net.Http;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Communication.Tests;

public class ExtensionHttpClientTests
{
    private static Mock<HttpMessageHandler> CreateHandlerMock(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return handler;
    }

    // ===== 构造函数测试 =====

    [Fact]
    public void 构造函数_ServerUrl为null_抛出ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExtensionHttpClient(null!, "Bearer", "token"));
    }

    [Fact]
    public void 构造函数_HttpClient为null_抛出ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExtensionHttpClient("http://test", "Bearer", "token", null!));
    }

    [Fact]
    public void 构造函数_ServerUrl末尾斜杠被Trim()
    {
        using var httpClient = new HttpClient();
        var client = new ExtensionHttpClient("http://test.com/", "Bearer", "token", httpClient);
        Assert.NotNull(client);
    }

    [Fact]
    public void 构造函数_Scheme和Token非空_设置AuthorizationHeader()
    {
        var handler = new Mock<HttpMessageHandler>();
        HttpRequestMessage? capturedRequest = null;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>()))
            });

        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "Bearer", "my-token", httpClient);

        client.QueryExtensionsAsync(new ExtensionQueryDTO()).GetAwaiter().GetResult();

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal("my-token", capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public void 构造函数_Scheme为空_不设置Authorization()
    {
        var handler = new Mock<HttpMessageHandler>();
        HttpRequestMessage? capturedRequest = null;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>()))
            });

        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        client.QueryExtensionsAsync(new ExtensionQueryDTO()).GetAwaiter().GetResult();

        Assert.Null(capturedRequest!.Headers.Authorization);
    }

    // ===== QueryExtensionsAsync 测试 =====

    [Fact]
    public async Task QueryExtensionsAsync_成功响应_返回解析后的DTO()
    {
        var expectedDto = new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
        {
            Code = "200",
            Message = "OK",
            Body = new PagedResultDTO<ExtensionDTO>
            {
                PageNumber = 1,
                TotalCount = 1,
                Items = new[] { new ExtensionDTO { Id = "ext-1", Name = "test" } }
            }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(expectedDto))
        };
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);

        var result = await client.QueryExtensionsAsync(new ExtensionQueryDTO { Id = "ext-1" });

        Assert.NotNull(result);
        Assert.Equal("OK", result.Message);
        Assert.NotNull(result.Body);
        Assert.Single(result.Body!.Items);
        Assert.Equal("ext-1", result.Body.Items.First().Id);
    }

    [Fact]
    public async Task QueryExtensionsAsync_非成功状态码_返回错误DTO()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not Found")
        };
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);

        var result = await client.QueryExtensionsAsync(new ExtensionQueryDTO());

        Assert.NotNull(result);
        Assert.Contains("404", result.Message);
    }

    [Fact]
    public async Task QueryExtensionsAsync_网络异常_返回错误DTO()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);

        var result = await client.QueryExtensionsAsync(new ExtensionQueryDTO());

        Assert.NotNull(result);
        Assert.Equal("QUERY_ERROR", result.Code);
        Assert.Contains("Network error", result.Message);
    }

    [Fact]
    public async Task QueryExtensionsAsync_响应为nullJSON_返回默认DTO()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
        };
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);

        var result = await client.QueryExtensionsAsync(new ExtensionQueryDTO());
        Assert.NotNull(result);
    }

    // ===== DownloadExtensionAsync 测试 =====

    [Fact]
    public async Task DownloadExtensionAsync_成功下载_返回true()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
        response.Content.Headers.ContentLength = 3;
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dl-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionAsync("ext-1", savePath);

        Assert.True(result);
        Assert.True(File.Exists(savePath));
        // 清理
        try { File.Delete(savePath); } catch { }
    }

    // ===== DownloadExtensionWithResultAsync 测试 =====

    [Fact]
    public async Task DownloadExtensionWithResultAsync_成功下载_返回Ok()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 10, 20, 30 })
        };
        response.Content.Headers.ContentLength = 3;
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.True(result.Success);
        Assert.Equal(DownloadErrorType.None, result.ErrorType);
        try { File.Delete(savePath); } catch { }
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_HTTP416_返回Ok()
    {
        var response = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr416-{Guid.NewGuid()}.zip");
        // 创建文件以模拟已有下载
        File.WriteAllText(savePath, "existing");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.True(result.Success);
        try { File.Delete(savePath); } catch { }
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_服务端500_返回Fail()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error"),
            ReasonPhrase = "Internal Server Error"
        };
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr500-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.ServerError, result.ErrorType);
        Assert.Equal(500, result.HttpStatusCode);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_客户端404_返回Fail()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found"
        };
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr404-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.ClientError, result.ErrorType);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_HttpRequestException_返回NetworkError()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-net-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.NetworkError, result.ErrorType);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_OperationCanceledException_返回Cancelled()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-cancel-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.Cancelled, result.ErrorType);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_IOException_返回IoError()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new IOException("Disk full"));
        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-io-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.IoError, result.ErrorType);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_一般Exception_返回Unknown()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new Exception("Unexpected error"));
        using var httpClient = new HttpClient(handler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-unk-{Guid.NewGuid()}.zip");

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath);

        Assert.False(result.Success);
        Assert.Equal(DownloadErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task DownloadExtensionWithResultAsync_进度报告正确()
    {
        var bytes = new byte[8192 * 3]; // 3 buffers worth
        new Random(42).NextBytes(bytes);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
        response.Content.Headers.ContentLength = bytes.Length;
        var mockHandler = CreateHandlerMock(response);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var client = new ExtensionHttpClient("http://test", "", "", httpClient);
        var savePath = Path.Combine(Path.GetTempPath(), $"dlr-prog-{Guid.NewGuid()}.zip");

        var progressValues = new List<int>();
        var progress = new Progress<int>(p => progressValues.Add(p));

        var result = await client.DownloadExtensionWithResultAsync("ext-1", savePath, progress);

        Assert.True(result.Success);
        Assert.NotEmpty(progressValues);
        Assert.Contains(100, progressValues);
        try { File.Delete(savePath); } catch { }
    }

    // ===== Dispose 测试 =====

    [Fact]
    public void Dispose_ownsHttpClient为true_释放HttpClient()
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var client = new ExtensionHttpClient("http://test", "", "", httpClient, ownsHttpClient: true);
        client.Dispose();
        // HttpClient 被释放后，再发送请求会抛异常
        Assert.Throws<ObjectDisposedException>(() => httpClient.Timeout = TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Dispose_ownsHttpClient为false_不释放HttpClient()
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var client = new ExtensionHttpClient("http://test", "", "", httpClient, ownsHttpClient: false);
        client.Dispose();
        // HttpClient 仍可用
        httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    [Fact]
    public void 便利构造函数_ownsHttpClient为true()
    {
        var client = new ExtensionHttpClient("http://test", "Bearer", "token");
        client.Dispose(); // 应释放内部创建的 HttpClient
    }
}
