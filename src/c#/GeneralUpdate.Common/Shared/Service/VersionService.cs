using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Shared.Service
{
    public class VersionService
    {
        private static readonly HttpClient _httpClient;

        static VersionService()
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = CheckValidationResult
            });
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml, */*");
        }

        public async Task<VersionRespDTO> ValidationVersion(string url)
        {
            var updateResp = await GetTaskAsync<VersionRespDTO>(url);
            if (updateResp == null || updateResp.Body == null)
            {
                throw new ArgumentNullException(
                    nameof(updateResp),
                    "The verification request is abnormal, please check the network or parameter configuration!"
                );
            }

            if (updateResp.Code == 200)
            {
                return updateResp;
            }
            else
            {
                throw new HttpRequestException(
                    $"Request failed, Code: {updateResp.Code}, Message: {updateResp.Message}!"
                );
            }
        }

        private async Task<T> GetTaskAsync<T>(string url, string headerKey = null, string headerValue = null)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(headerKey) && !string.IsNullOrEmpty(headerValue))
                {
                    request.Headers.Add(headerKey, headerValue);
                }

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode(); // Throw if not a success code.
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<T>(responseContent);

                return result;
            }
            catch (Exception ex)
            {
                // Log the exception here as needed
                return default;
            }
        }

        private static bool CheckValidationResult(
            HttpRequestMessage message,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) => true;
    }
}