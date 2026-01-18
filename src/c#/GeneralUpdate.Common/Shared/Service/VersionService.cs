using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Shared.Service
{
    public class VersionService
    {
        private VersionService() { }
        
        /// <summary>
        /// Report the result of this update: whether it was successful.
        /// </summary>
        /// <param name="httpUrl"></param>
        /// <param name="recordId"></param>
        /// <param name="status"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static async Task Report(string httpUrl
            , int recordId
            , int status
            , int? type
            , string scheme = null
            , string token = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "RecordId", recordId },
                { "Status", status },
                { "Type", type }
            };
            await PostTaskAsync<BaseResponseDTO<bool>>(httpUrl, parameters, ReportRespJsonContext.Default.BaseResponseDTOBoolean, scheme, token);
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
            , int platform
            , string productId
            , string scheme = null
            , string token = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Version", version },
                { "AppType", appType },
                { "AppKey", appKey },
                { "Platform", platform },
                { "ProductId", productId }
            };
            return await PostTaskAsync<VersionRespDTO>(httpUrl, parameters, VersionRespJsonContext.Default.VersionRespDTO, scheme, token);
        }

        /// <summary>
        /// Validate and retrieve available plugin updates for the current client version.
        /// </summary>
        /// <param name="httpUrl">Plugin validation API endpoint.</param>
        /// <param name="clientVersion">Current client version.</param>
        /// <param name="pluginId">Optional plugin ID to check specific plugin.</param>
        /// <param name="appKey">Application secret key.</param>
        /// <param name="platform">Platform type (Windows/Linux).</param>
        /// <param name="productId">Product identifier.</param>
        /// <param name="scheme">Authentication scheme.</param>
        /// <param name="token">Authentication token.</param>
        /// <returns>PluginRespDTO containing available plugin updates.</returns>
        public static async Task<PluginRespDTO> ValidatePlugins(string httpUrl
            , string clientVersion
            , string pluginId = null
            , string appKey = null
            , int platform = 0
            , string productId = null
            , string scheme = null
            , string token = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "ClientVersion", clientVersion },
                { "Platform", platform }
            };

            if (!string.IsNullOrEmpty(pluginId))
                parameters.Add("PluginId", pluginId);
            
            if (!string.IsNullOrEmpty(appKey))
                parameters.Add("AppKey", appKey);
            
            if (!string.IsNullOrEmpty(productId))
                parameters.Add("ProductId", productId);

            return await PostTaskAsync<PluginRespDTO>(httpUrl, parameters, PluginRespJsonContext.Default.PluginRespDTO, scheme, token);
        }

        private static async Task<T> PostTaskAsync<T>(string httpUrl, Dictionary<string, object> parameters, JsonTypeInfo<T>? typeInfo = null, string scheme = null, string token = null)
        {
            try
            {
                var uri = new Uri(httpUrl);
                using var httpClient = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = CheckValidationResult
                });
                httpClient.Timeout = TimeSpan.FromSeconds(15);
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml, */*");
                
                if (!string.IsNullOrEmpty(scheme) && !string.IsNullOrEmpty(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue(scheme, token);
                }
                
                var parametersJson =
                    JsonSerializer.Serialize(parameters, HttpParameterJsonContext.Default.DictionaryStringObject);
                var stringContent = new StringContent(parametersJson, Encoding.UTF8, "application/json");
                var result = await httpClient.PostAsync(uri, stringContent);
                var reseponseJson = await result.Content.ReadAsStringAsync();
                return typeInfo == null
                    ? JsonSerializer.Deserialize<T>(reseponseJson)
                    : JsonSerializer.Deserialize(reseponseJson, typeInfo);
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The PostTaskAsync method in the VersionService class throws an exception.", e);
                throw e;
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