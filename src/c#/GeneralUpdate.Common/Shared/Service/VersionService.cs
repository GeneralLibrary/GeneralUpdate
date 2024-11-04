using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Shared.Service
{
    public class VersionService
    {
        /// <summary>
        /// Report the result of this update: whether it was successful.
        /// </summary>
        /// <param name="httpUrl"></param>
        /// <param name="recordId"></param>
        /// <param name="status"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static async Task<BaseResponseDTO<bool>> Report(string httpUrl
            , int recordId
            , int status
            , int? type)
        {
            var parameters = new Dictionary<string, object>
            {
                { "RecordId", recordId },
                { "Status", status },
                { "Type", type }
            };
            return await PostTaskAsync<BaseResponseDTO<bool>>(httpUrl, parameters);
        }

        /// <summary>
        /// Verify whether the current version needs an update.
        /// </summary>
        /// <param name="httpUrl"></param>
        /// <param name="version"></param>
        /// <param name="appType"></param>
        /// <param name="appKey"></param>
        /// <param name="platform"></param>
        /// <param name="productId"></param>
        /// <returns></returns>
        public static async Task<VersionRespDTO> Validate(string httpUrl
            , string version
            , int appType
            , string appKey
            , int platform,
            string productId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Version", version },
                { "AppType", appType },
                { "AppKey", appKey },
                { "Platform", platform },
                { "ProductId", productId }
            };
            return await PostTaskAsync<VersionRespDTO>(httpUrl, parameters);
        }

        private static async Task<T> PostTaskAsync<T>(string httpUrl, Dictionary<string, object> parameters)
        {
            var uri = new Uri(httpUrl);
            using var httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = CheckValidationResult
            });
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml, */*");
            string parametersJson = JsonSerializer.Serialize(parameters);
            var stringContent = new StringContent(parametersJson, Encoding.UTF8, "application/json");
            var result = await httpClient.PostAsync(uri, stringContent);
            var reseponseJson = await result.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(reseponseJson);
        }

        private static bool CheckValidationResult(
            HttpRequestMessage message,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) => true;
    }
}