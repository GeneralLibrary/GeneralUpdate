using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private static readonly HttpClientHandler _sharedHandler;
        private static readonly HttpClient _sharedClient;

        private readonly IHttpAuthProvider _authProvider;
        private readonly ISslValidationPolicy _sslPolicy;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Default SSL validation policy used across all HTTP requests.
        /// Can be overridden globally via <see cref="SetSslValidationPolicy"/>.
        /// </summary>
        private static ISslValidationPolicy _globalSslPolicy = new StrictSslValidationPolicy();

        private static volatile bool _handlerInitialized;

        static VersionService()
        {
            _sharedHandler = new HttpClientHandler();
            _sharedHandler.ServerCertificateCustomValidationCallback = SharedCertificateValidation;
            _sharedClient = new HttpClient(_sharedHandler, disposeHandler: false);
        }

        /// <summary>
        /// Set a global SSL validation policy for all VersionService HTTP requests.
        /// Must be called before the first HTTP request is made.
        /// </summary>
        public static void SetSslValidationPolicy(ISslValidationPolicy policy)
        {
            _globalSslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            _handlerInitialized = true;
        }

        private static bool SharedCertificateValidation(
            HttpRequestMessage message,
            X509Certificate2? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return _globalSslPolicy.ValidateCertificate(certificate, chain, sslPolicyErrors);
        }

        // ════════════════════════════════════════════════════════════
        // Instance (with custom auth/timeout)
        // ════════════════════════════════════════════════════════════
        public VersionService(
            IHttpAuthProvider? authProvider = null,
            ISslValidationPolicy? sslPolicy = null,
            TimeSpan? timeout = null)
        {
            _authProvider = authProvider ?? new NoOpAuthProvider();
            _sslPolicy = sslPolicy ?? _globalSslPolicy;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
        }

        private VersionService() { }

        // ════════════════════════════════════════════════════════════
        // Static convenience methods (backward-compatible API surface)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Report the result of this update: whether it was successful.
        /// </summary>
        public static Task Report(string httpUrl
            , int recordId
            , int status
            , int? type
            , string scheme = null
            , string token = null)
        {
            var auth = HttpAuthProviderFactory.Create(scheme, token, null);
            var svc = new VersionService(auth);
            return svc.ReportAsync(httpUrl, recordId, status, type);
        }

        /// <summary>
        /// Verify whether the current version needs an update.
        /// </summary>
        public static Task<VersionRespDTO> Validate(string httpUrl
            , string version
            , int appType
            , string appKey
            , int platform
            , string productId
            , string scheme = null
            , string token = null)
        {
            var auth = HttpAuthProviderFactory.Create(scheme, token, appKey);
            var svc = new VersionService(auth);
            return svc.ValidateAsync(httpUrl, version, appType, platform, productId);
        }

        // ════════════════════════════════════════════════════════════
        // Instance methods (with retry support)
        // ════════════════════════════════════════════════════════════

        private async Task ReportAsync(string httpUrl
            , int recordId
            , int status
            , int? type
            , CancellationToken token = default)
        {
            var parameters = new Dictionary<string, object>
            {
                { "RecordId", recordId },
                { "Status", status },
                { "Type", type }
            };
            await PostTaskAsync<BaseResponseDTO<bool>>(
                httpUrl, parameters, ReportRespJsonContext.Default.BaseResponseDTOBoolean, token)
                .ConfigureAwait(false);
        }

        private async Task<VersionRespDTO> ValidateAsync(string httpUrl
            , string version
            , int appType
            , int platform
            , string productId
            , CancellationToken token = default)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Version", version },
                { "AppType", appType },
                { "Platform", platform },
                { "ProductId", productId }
            };
            return await PostTaskAsync<VersionRespDTO>(
                httpUrl, parameters, VersionRespJsonContext.Default.VersionRespDTO, token)
                .ConfigureAwait(false);
        }

        private async Task<T> PostTaskAsync<T>(
            string httpUrl,
            Dictionary<string, object> parameters,
            JsonTypeInfo<T>? typeInfo = null,
            CancellationToken token = default,
            int maxRetries = 3)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await SendRequestAsync<T>(httpUrl, parameters, typeInfo, token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maxRetries - 1 && IsRetryable(ex))
                {
                    GeneralTracer.Warn(
                        $"HTTP request attempt {attempt + 1}/{maxRetries} failed, retrying... Details: {ex.Message}");

                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000);
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
            }
        }

        private async Task<T> SendRequestAsync<T>(
            string httpUrl,
            Dictionary<string, object> parameters,
            JsonTypeInfo<T>? typeInfo,
            CancellationToken token)
        {
            var uri = new Uri(httpUrl);

            using var request = new HttpRequestMessage(HttpMethod.Post, uri);

            // Apply auth
            await _authProvider.ApplyAuthAsync(request, token).ConfigureAwait(false);

            // Build body
            var parametersJson = JsonSerializer.Serialize(
                parameters, HttpParameterJsonContext.Default.DictionaryStringObject);
            request.Content = new StringContent(parametersJson, Encoding.UTF8, "application/json");
            request.Headers.Accept.ParseAdd("application/json");

            // Re-apply auth after setting content (HMAC needs the body)
            await _authProvider.ApplyAuthAsync(request, token).ConfigureAwait(false);

            // Send via shared HttpClient with custom timeout
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

        /// <summary>
        /// Determine if an exception is worth retrying.
        /// Only retry on transient failures (network, timeout, server errors).
        /// Do NOT retry on SSL/authentication failures — those are permanent.
        /// </summary>
        private static bool IsRetryable(Exception ex)
        {
            if (ex is OperationCanceledException)
                return false;

            if (ex is HttpRequestException hre)
            {
                // Retry on timeout and server errors, not on client/SSL errors
                var msg = hre.Message ?? string.Empty;
                return msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("server", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("500")
                    || msg.Contains("502")
                    || msg.Contains("503")
                    || msg.Contains("504");
            }

            if (ex is TaskCanceledException)
                return true; // Timeout

            if (ex is TimeoutException)
                return true;

            // IOException = network interruption
            if (ex is System.IO.IOException)
                return true;

            return false;
        }
    }
}
