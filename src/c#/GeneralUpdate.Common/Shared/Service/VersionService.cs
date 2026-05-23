using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Security;

namespace GeneralUpdate.Common.Shared.Service
{
    public class VersionService
    {
        private static readonly HttpClient _sharedClient;
        private static ISslValidationPolicy _globalSslPolicy = new StrictSslValidationPolicy();

        private readonly IHttpAuthProvider _authProvider;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        static VersionService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = SharedCertValidation;
            _sharedClient = new HttpClient(handler, disposeHandler: false);
        }

        public static void SetSslValidationPolicy(ISslValidationPolicy policy)
        {
            _globalSslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        private static bool SharedCertValidation(
            HttpRequestMessage message,
            X509Certificate2? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return _globalSslPolicy.ValidateCertificate(certificate, chain, sslPolicyErrors);
        }

        public VersionService(
            IHttpAuthProvider? authProvider = null,
            TimeSpan? timeout = null,
            int maxRetries = 3)
        {
            _authProvider = authProvider ?? new NoOpAuthProvider();
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _maxRetries = maxRetries;
        }

        private VersionService() { }

        // ═══════════ Static API (backward-compatible) ═══════════

        public static Task Report(string httpUrl, int recordId, int status, int? type,
            string scheme = null, string token = null)
        {
            var auth = HttpAuthProviderFactory.Create(scheme, token, null);
            var svc = new VersionService(auth);
            return svc.ReportAsync(httpUrl, recordId, status, type);
        }

        public static Task<VersionRespDTO> Validate(string httpUrl, string version,
            int appType, string appKey, int platform, string productId,
            string scheme = null, string token = null)
        {
            var auth = HttpAuthProviderFactory.Create(scheme, token, appKey);
            var svc = new VersionService(auth);
            return svc.ValidateAsync(httpUrl, version, appType, platform, productId);
        }

        // ═══════════ Instance methods ═══════════

        private async Task ReportAsync(string httpUrl, int recordId, int status,
            int? type, CancellationToken token = default)
        {
            var p = new Dictionary<string, object> {
                { "RecordId", recordId }, { "Status", status }, { "Type", type }
            };
            await PostAsync<BaseResponseDTO<bool>>(
                httpUrl, p, ReportRespJsonContext.Default.BaseResponseDTOBoolean, token)
                .ConfigureAwait(false);
        }

        private async Task<VersionRespDTO> ValidateAsync(string httpUrl, string version,
            int appType, int platform, string productId, CancellationToken token = default)
        {
            var p = new Dictionary<string, object> {
                { "Version", version }, { "AppType", appType },
                { "Platform", platform }, { "ProductId", productId }
            };
            return await PostAsync<VersionRespDTO>(
                httpUrl, p, VersionRespJsonContext.Default.VersionRespDTO, token)
                .ConfigureAwait(false);
        }

        private async Task<T> PostAsync<T>(string httpUrl, Dictionary<string, object> parameters,
            JsonTypeInfo<T>? typeInfo, CancellationToken token, int maxRetriesOverride = 0)
        {
            int max = maxRetriesOverride > 0 ? maxRetriesOverride : _maxRetries;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await SendAsync<T>(httpUrl, parameters, typeInfo, token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < max - 1 && IsRetryable(ex))
                {
                    GeneralTracer.Warn(
                        $"HTTP attempt {attempt + 1}/{max} failed, retrying. {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), token)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task<T> SendAsync<T>(string httpUrl, Dictionary<string, object> parameters,
            JsonTypeInfo<T>? typeInfo, CancellationToken token)
        {
            var uri = new Uri(httpUrl);
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Accept.ParseAdd("application/json");

            var json = JsonSerializer.Serialize(
                parameters, HttpParameterJsonContext.Default.DictionaryStringObject);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await _authProvider.ApplyAuthAsync(request, token).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_timeout);

            var response = await _sharedClient.SendAsync(request, timeoutCts.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);
            return typeInfo == null
                ? JsonSerializer.Deserialize<T>(responseJson)
                : JsonSerializer.Deserialize(responseJson, typeInfo);
        }

        private static bool IsRetryable(Exception ex)
        {
            if (ex is OperationCanceledException) return false;
            if (ex is TaskCanceledException) return true;
            if (ex is TimeoutException) return true;
            if (ex is System.IO.IOException) return true;
            if (ex is HttpRequestException hre)
            {
                var msg = hre.Message ?? "";
                return msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("500") || msg.Contains("502")
                    || msg.Contains("503") || msg.Contains("504");
            }
            return false;
        }
    }
}
