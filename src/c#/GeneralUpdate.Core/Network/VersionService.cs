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
using GeneralUpdate.Core;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network
{
    public class VersionService
    {
        private static readonly HttpClient _sharedClient;
        private static ISslValidationPolicy _globalSslPolicy = new StrictSslValidationPolicy();

        private readonly IHttpAuthProvider _auth;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        static VersionService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = SharedCertValidation;
            _sharedClient = new HttpClient(handler, disposeHandler: false);
        }

        public static void SetSslValidationPolicy(ISslValidationPolicy policy)
            => _globalSslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));

        private static bool SharedCertValidation(HttpRequestMessage m, X509Certificate2? c,
            X509Chain? ch, SslPolicyErrors e)
            => _globalSslPolicy.ValidateCertificate(c, ch, e);

        public VersionService(IHttpAuthProvider? auth = null, TimeSpan? timeout = null, int maxRetries = 3)
        {
            _auth = auth ?? new NoOpAuthProvider();
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _maxRetries = maxRetries;
        }
        private VersionService() { }

        // Static API (backward-compatible, CancellationToken optional)
        public static Task Report(string url, int recordId, int status, int? type,
            string scheme = null, string token = null, CancellationToken ct = default)
        {
            var a = HttpAuthProviderFactory.Create(scheme, token, null);
            return new VersionService(a).ReportAsync(url, recordId, status, type, ct);
        }

        public static Task<VersionRespDTO> Validate(string url, string version,
            int appType, string appKey, int platform, string productId,
            string scheme = null, string token = null, CancellationToken ct = default)
        {
            var a = HttpAuthProviderFactory.Create(scheme, token, appKey);
            return new VersionService(a).ValidateAsync(url, version, appType, platform, productId, ct);
        }

        private async Task ReportAsync(string url, int recordId, int status, int? type, CancellationToken t = default)
        {
            var p = new Dictionary<string, object> { ["RecordId"] = recordId, ["Status"] = status, ["Type"] = type };
            await PostAsync<BaseResponseDTO<bool>>(url, p, ReportRespJsonContext.Default.BaseResponseDTOBoolean, t);
        }

        private async Task<VersionRespDTO> ValidateAsync(string url, string v, int at, int pf, string pid,
            CancellationToken t = default)
        {
            var p = new Dictionary<string, object> { ["Version"] = v, ["AppType"] = at, ["Platform"] = pf, ["ProductId"] = pid };
            return await PostAsync<VersionRespDTO>(url, p, VersionRespJsonContext.Default.VersionRespDTO, t);
        }

        private async Task<T> PostAsync<T>(string url, Dictionary<string, object> p,
            JsonTypeInfo<T>? ti, CancellationToken t)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await SendAsync<T>(url, p, ti, t).ConfigureAwait(false); }
                catch (Exception ex) when (attempt < _maxRetries - 1 && IsRetryable(ex))
                {
                    GeneralTracer.Warn($"HTTP attempt {attempt + 1}/{_maxRetries} failed, retrying. {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), t).ConfigureAwait(false);
                }
            }
        }

        private async Task<T> SendAsync<T>(string url, Dictionary<string, object> p,
            JsonTypeInfo<T>? ti, CancellationToken t)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
            req.Headers.Accept.ParseAdd("application/json");
            var json = JsonSerializer.Serialize(p, HttpParameterJsonContext.Default.DictionaryStringObject);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            await _auth.ApplyAuthAsync(req, t).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(t);
            cts.CancelAfter(_timeout);
            var r = await _sharedClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            r.EnsureSuccessStatusCode();
            var rj = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ti == null ? JsonSerializer.Deserialize<T>(rj) : JsonSerializer.Deserialize(rj, ti);
        }

        private static bool IsRetryable(Exception ex)
        {
            if (ex is OperationCanceledException) return false;
            if (ex is TaskCanceledException or TimeoutException or System.IO.IOException) return true;
            if (ex is HttpRequestException h && (h.Message ?? "").Contains("timeout", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
