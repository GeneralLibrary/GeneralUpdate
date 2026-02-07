using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Common.DTOs;
using Newtonsoft.Json;

namespace GeneralUpdate.Extension.Communication;

/// <summary>
/// HTTP client for extension API communication
/// </summary>
public class ExtensionHttpClient : IExtensionHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;

    /// <summary>
    /// Initialize extension HTTP client
    /// </summary>
    /// <param name="serverUrl">Server base URL</param>
    public ExtensionHttpClient(string serverUrl, string scheme, string token)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = new HttpClient();
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
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startPosition, null);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return true;
            }
            
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                return false;
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

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
