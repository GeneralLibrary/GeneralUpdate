using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Models;
using Newtonsoft.Json;

namespace GeneralUpdate.Extension.Communication;

/// <summary>
/// HTTP client for extension API communication
/// </summary>
public class ExtensionHttpClient : IExtensionHttpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Initialize extension HTTP client (convenience constructor — creates its own HttpClient).
    /// Prefer the <see cref="ExtensionHttpClient(string,string,string,HttpClient)"/> overload
    /// that accepts an externally managed <see cref="HttpClient"/> for better connection pooling.
    /// </summary>
    /// <param name="serverUrl">Server base URL</param>
    /// <param name="scheme">Authentication scheme (e.g., "Bearer")</param>
    /// <param name="token">Authentication token</param>
    public ExtensionHttpClient(string serverUrl, string scheme, string token)
        : this(serverUrl, scheme, token, new HttpClient(), ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Initialize extension HTTP client with an externally managed <see cref="HttpClient"/>.
    /// Use this overload with <see cref="IHttpClientFactory"/> for optimal connection management.
    /// </summary>
    /// <param name="serverUrl">Server base URL</param>
    /// <param name="scheme">Authentication scheme (e.g., "Bearer")</param>
    /// <param name="token">Authentication token</param>
    /// <param name="httpClient">Externally managed HttpClient instance</param>
    /// <param name="ownsHttpClient">If true, the HttpClient will be disposed when this instance is disposed</param>
    public ExtensionHttpClient(string serverUrl, string scheme, string token, HttpClient httpClient, bool ownsHttpClient = false)
    {
        _serverUrl = (serverUrl ?? throw new ArgumentNullException(nameof(serverUrl))).TrimEnd('/');
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;

        if (!string.IsNullOrWhiteSpace(scheme) && !string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue(scheme, token);
        }
    }

    public async Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryExtensionsAsync(
        ExtensionQueryDTO query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/Query";
            var json = JsonConvert.SerializeObject(query);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // IMPORTANT: The server API specification explicitly requires [HttpGet] with [FromBody]
            // This is non-standard HTTP practice, but we must follow the server API contract.
            // Most modern HTTP clients (including HttpClient) support this, though some proxies may not.
            // If compatibility issues arise, coordinate with the server team to change to POST or query parameters.
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>>(responseJson);
                return result ?? new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
                {
                    Message = "Failed to deserialize response"
                };
            }

            return new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Message = $"HTTP {response.StatusCode}: {responseJson}"
            };
        }
        catch (Exception ex)
        {
            return new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Message = $"Query failed: {ex.Message}",
                Code = "QUERY_ERROR"
            };
        }
    }

    public async Task<bool> DownloadExtensionAsync(
        string extensionId, 
        string savePath, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DownloadExtensionWithResultAsync(extensionId, savePath, progress, cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Download an extension package with detailed error classification.
    /// </summary>
    public async Task<DownloadResult> DownloadExtensionWithResultAsync(
        string extensionId,
        string savePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/Download/{extensionId}";

            // Support resume by checking existing file
            long startPosition = 0;
            FileMode fileMode = FileMode.Create;

            if (File.Exists(savePath))
            {
                var fileInfo = new FileInfo(savePath);
                startPosition = fileInfo.Length;
                fileMode = FileMode.Append;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (startPosition > 0)
            {
                request.Headers.Range = new RangeHeaderValue(startPosition, null);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return DownloadResult.Ok(); // Already fully downloaded
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
            {
                var errorType = (int)response.StatusCode >= 500
                    ? DownloadErrorType.ServerError
                    : DownloadErrorType.ClientError;

                return DownloadResult.Fail(
                    errorType,
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = startPosition;

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(savePath, fileMode, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (int)((downloadedBytes * 100) / (totalBytes + startPosition));
                        progress?.Report(percentage);
                    }
                }
            }

            return DownloadResult.Ok();
        }
        catch (OperationCanceledException)
        {
            return DownloadResult.Fail(DownloadErrorType.Cancelled, "Download was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return DownloadResult.Fail(DownloadErrorType.NetworkError, ex.Message);
        }
        catch (IOException ex)
        {
            return DownloadResult.Fail(DownloadErrorType.IoError, ex.Message);
        }
        catch (Exception ex)
        {
            return DownloadResult.Fail(DownloadErrorType.Unknown, ex.Message);
        }
    }

    /// <summary>
    /// Disposes the internally managed HttpClient if this instance owns it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
