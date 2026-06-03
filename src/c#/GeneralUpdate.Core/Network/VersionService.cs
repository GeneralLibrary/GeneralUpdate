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
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network
{
    /// <summary>
    /// Version service providing HTTP communication with the update server,
    /// including version validation and status reporting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is the HTTP communication layer of the GeneralUpdate framework.
    /// Its key design points are as follows:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Uses a static shared <see cref="HttpClient"/> instance (<c>_sharedClient</c>)
    ///     to avoid socket exhaustion, and supports configurable SSL certificate validation
    ///     policies via <see cref="SetSslValidationPolicy"/>.</description>
    ///   </item>
    ///   <item>
    ///     <description>Provides two sets of static convenience APIs
    ///     (<see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
    ///     and <see cref="Report(string, int, int, int?, string, string, CancellationToken)"/>),
    ///     which internally create instances and call the corresponding async methods.
    ///     These static methods are retained for backward compatibility.</description>
    ///   </item>
    ///   <item>
    ///     <description>Supports a pluggable authentication provider (<see cref="IHttpAuthProvider"/>)
    ///     with built-in support for Bearer Token, API Key, HMAC, and extensibility
    ///     through <see cref="HttpAuthProviderFactory"/>.</description>
    ///   </item>
    ///   <item>
    ///     <description>Implements exponential backoff retry: in <see cref="PostAsync{T}"/>,
    ///     retryable exceptions trigger a wait of 2^attempt * 1000 milliseconds.</description>
    ///   </item>
    ///   <item>
    ///     <description>Supports global SSL policy (via <see cref="SetSslValidationPolicy"/>)
    ///     and global authentication provider (via <see cref="SetDefaultAuthProvider"/>).
    ///     When a global auth provider is set, it overrides the factory method
    ///     <see cref="HttpAuthProviderFactory.Create"/>.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Typical usage scenarios:
    /// <list type="bullet">
    ///   <item><description>At startup, call <see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
    ///   to check whether the server has a new version.</description></item>
    ///   <item><description>After download completes, use <see cref="Download.Reporting.IUpdateReporter.ReportAsync"/>
    ///   to report the update status.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class VersionService
    {
        private static readonly HttpClient _sharedClient;
        private static ISslValidationPolicy _globalSslPolicy = new StrictSslValidationPolicy();
        private static IHttpAuthProvider? _globalAuthProvider;

        private readonly IHttpAuthProvider _auth;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        /// <summary>
        /// Static constructor: initializes the static members of <see cref="VersionService"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Execution flow:
        /// <list type="number">
        ///   <item><description>Creates an <see cref="HttpClientHandler"/> with a custom SSL validation callback.</description></item>
        ///   <item><description>The SSL validation logic is delegated to <see cref="ISslValidationPolicy"/>,
        ///   which can be replaced globally via <see cref="SetSslValidationPolicy"/>.</description></item>
        ///   <item><description>Initializes the static shared <see cref="HttpClient"/> instance using the handler.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        static VersionService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = SharedCertValidation;
            _sharedClient = new HttpClient(handler, disposeHandler: false);
        }

        /// <summary>
        /// Sets the global SSL certificate validation policy.
        /// </summary>
        /// <remarks>
        /// This policy affects all HTTPS requests made by <see cref="VersionService"/> instances.
        /// The default is <see cref="StrictSslValidationPolicy"/>, i.e., strict mode.
        /// Pass a custom <see cref="ISslValidationPolicy"/> implementation to relax or replace
        /// the validation logic.
        /// </remarks>
        /// <param name="policy">The SSL validation policy instance. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
        public static void SetSslValidationPolicy(ISslValidationPolicy policy)
            => _globalSslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));

        /// <summary>
        /// Sets the global default HTTP authentication provider.
        /// </summary>
        /// <remarks>
        /// When a global authentication provider is set, all requests made via the static APIs
        /// (<see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
        /// and <see cref="Report(string, int, int, int?, string, string, CancellationToken)"/>)
        /// will preferentially use this provider, overriding the authentication instance
        /// created by <see cref="HttpAuthProviderFactory.Create"/>.
        /// <para>
        /// Passing null clears the global authentication provider, reverting to the factory method.
        /// </para>
        /// </remarks>
        /// <param name="provider">The global authentication provider instance, or null to clear the global configuration.</param>
        public static void SetDefaultAuthProvider(IHttpAuthProvider? provider)
            => _globalAuthProvider = provider;

        private static bool SharedCertValidation(HttpRequestMessage m, X509Certificate2? c,
            X509Chain? ch, SslPolicyErrors e)
            => _globalSslPolicy.ValidateCertificate(c, ch, e);

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionService"/> class.
        /// </summary>
        /// <remarks>
        /// Instance methods (<see cref="ValidateAsync"/> and <see cref="ReportAsync"/>) use
        /// this instance's authentication provider and timeout settings.
        /// When <paramref name="auth"/> is null, <see cref="NoOpAuthProvider"/> (no authentication) is used by default.
        /// </remarks>
        /// <param name="auth">The HTTP authentication provider. If null, <see cref="NoOpAuthProvider"/> is used.</param>
        /// <param name="timeout">The request timeout. If null, defaults to 30 seconds.</param>
        /// <param name="maxRetries">The maximum number of retry attempts. Defaults to 3.</param>
        public VersionService(IHttpAuthProvider? auth = null, TimeSpan? timeout = null, int maxRetries = 3)
        {
            _auth = auth ?? new NoOpAuthProvider();
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _maxRetries = maxRetries;
        }

        /// <summary>
        /// Validates the current version against the server to check for available updates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the recommended strongly-typed overload. It internally creates a
        /// <see cref="VersionService"/> instance and calls <see cref="ValidateAsync"/>.
        /// </para>
        /// <para>
        /// Execution flow:
        /// <list type="number">
        ///   <item><description>Resolves the authentication provider: uses the global provider
        ///   (<see cref="SetDefaultAuthProvider"/>) first; otherwise creates one via
        ///   <see cref="HttpAuthProviderFactory.Create"/>.</description></item>
        ///   <item><description>Creates a temporary <see cref="VersionService"/> instance.</description></item>
        ///   <item><description>Constructs request parameters containing the version, app type, platform, etc.</description></item>
        ///   <item><description>Sends the parameters to the server via a POST request and deserializes
        ///   the response into a <see cref="VersionRespDTO"/>.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="url">The server version validation API URL.</param>
        /// <param name="version">The current client version string.</param>
        /// <param name="appType">The application type (e.g., main program, patch, etc.).</param>
        /// <param name="appKey">The application key used for server-side authentication.</param>
        /// <param name="platform">The target platform (Windows, Linux, macOS, etc.).</param>
        /// <param name="productId">The product identifier.</param>
        /// <param name="scheme">The authentication scheme (e.g., "bearer", "apikey", "hmac"), used to create the auth provider. Ignored when a global auth provider is set.</param>
        /// <param name="token">The authentication token or key, used together with <paramref name="scheme"/>.</param>
        /// <param name="authScheme">Explicitly selects the HTTP authentication method. Defaults to <see cref="Security.AuthScheme.Hmac"/>.</param>
        /// <param name="basicUsername">The username for HTTP Basic Authentication. Used when <see cref="Security.AuthScheme.Basic"/> is selected.</param>
        /// <param name="basicPassword">The password for HTTP Basic Authentication. Used when <paramref name="scheme"/> is "basic".</param>
        /// <param name="ct">A <see cref="CancellationToken"/> for cancelling the operation.</param>
        /// <returns>A <see cref="VersionRespDTO"/> containing the version validation result
        /// (e.g., whether an update exists, download URL, etc.).</returns>
        public static Task<VersionRespDTO> Validate(string url, string version,
            AppType appType, string appKey, PlatformType platform, string productId,
            string scheme = null, string token = null, Security.AuthScheme authScheme = Security.AuthScheme.Hmac, string basicUsername = null, string basicPassword = null, CancellationToken ct = default)
        {
            var auth = _globalAuthProvider ?? HttpAuthProviderFactory.Create(scheme, token, appKey, authScheme, basicUsername, basicPassword);
            return new VersionService(auth).ValidateAsync(url, version, (int)appType, appKey, (int)platform, productId, ct);
        }

        /// <summary>
        /// Validates the current version against the server (backward-compatible overload using integer parameters).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This overload converts the integer parameters to their corresponding enum types
        /// and delegates to the strongly-typed overload
        /// <see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>.
        /// Retained for binary compatibility with older callers.
        /// </para>
        /// </remarks>
        /// <param name="url">The server version validation API URL.</param>
        /// <param name="version">The current client version string.</param>
        /// <param name="appType">The application type (as an integer, will be cast to <see cref="AppType"/>).</param>
        /// <param name="appKey">The application key.</param>
        /// <param name="platform">The target platform (as an integer, will be cast to <see cref="PlatformType"/>).</param>
        /// <param name="productId">The product identifier.</param>
        /// <param name="scheme">The authentication scheme.</param>
        /// <param name="token">The authentication token or key.</param>
        /// <param name="authScheme">Explicitly selects the HTTP authentication method.</param>
        /// <param name="basicUsername">The username for HTTP Basic Authentication.</param>
        /// <param name="basicPassword">The password for HTTP Basic Authentication.</param>
        /// <param name="ct">A <see cref="CancellationToken"/> for cancelling the operation.</param>
        /// <returns>A <see cref="VersionRespDTO"/> containing the version validation result.</returns>
        public static Task<VersionRespDTO> Validate(string url, string version,
            int appType, string appKey, int platform, string productId,
            string scheme = null, string token = null, Security.AuthScheme authScheme = Security.AuthScheme.Hmac, string basicUsername = null, string basicPassword = null, CancellationToken ct = default)
            => Validate(url, version, (AppType)appType, appKey, (PlatformType)platform, productId, scheme, token, authScheme, basicUsername, basicPassword, ct);

        /// <summary>
        /// Asynchronously validates the version by querying the server for available updates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Execution flow:
        /// <list type="number">
        ///   <item><description>Constructs a parameter dictionary with the version, app type, app key,
        ///   platform, product ID, and upgrade mode.</description></item>
        ///   <item><description>Sends the parameters via a POST request using <see cref="PostAsync{T}"/>.</description></item>
        ///   <item><description>Deserializes the response into a <see cref="VersionRespDTO"/>.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="url">The server version validation API URL.</param>
        /// <param name="v">The current client version string.</param>
        /// <param name="at">The application type as an integer value.</param>
        /// <param name="appKey">The application key.</param>
        /// <param name="pf">The platform type as an integer value.</param>
        /// <param name="pid">The product identifier.</param>
        /// <param name="t">A <see cref="CancellationToken"/> for cancelling the operation.</param>
        /// <returns>A <see cref="VersionRespDTO"/> containing the version validation result.</returns>
        private async Task<VersionRespDTO> ValidateAsync(string url, string v, int at, string appKey, int pf, string pid,
            CancellationToken t = default)
        {
            var p = new Dictionary<string, object> { ["version"] = v, ["appType"] = at, ["appKey"] = appKey, ["platform"] = pf, ["productId"] = pid, ["upgradeMode"] = 1 };
            return await PostAsync<VersionRespDTO>(url, p, VersionRespJsonContext.Default.VersionRespDTO, t);
        }

        /// <summary>
        /// Executes an HTTP POST request with exponential backoff retry logic.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method encapsulates the retry logic. The execution flow is as follows:
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>Calls <see cref="SendAsync{T}"/> to send the POST request.</description>
        ///   </item>
        ///   <item>
        ///     <description>If the request succeeds, the deserialized result is returned directly.</description>
        ///   </item>
        ///   <item>
        ///     <description>If a retryable exception (see <see cref="IsRetryable"/>) is thrown and the
        ///     maximum retry count has not been reached, waits for an exponentially increasing
        ///     interval (2^attempt * 1000 milliseconds) before retrying.</description>
        ///   </item>
        ///   <item>
        ///     <description>Non-retryable exceptions (such as <see cref="OperationCanceledException"/>)
        ///     propagate immediately.</description>
        ///   </item>
        /// </list>
        /// <para>
        /// During retry waits, the thread is released via
        /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/>, and the cancellation token is respected.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The deserialization target type for the response data.</typeparam>
        /// <param name="url">The target URL for the request.</param>
        /// <param name="p">The POST body parameter dictionary.</param>
        /// <param name="ti">The JSON type info metadata for source generator (may be null, in which case reflection-based deserialization is used).</param>
        /// <param name="t">A <see cref="CancellationToken"/> for cancelling the operation.</param>
        /// <returns>The deserialized response data.</returns>
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

        /// <summary>
        /// Executes a single HTTP POST request, including authentication injection and timeout control.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method handles the full lifecycle of a single HTTP request:
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>Constructs an <see cref="HttpRequestMessage"/> with the URL, method (POST),
        ///     and Accept header.</description>
        ///   </item>
        ///   <item>
        ///     <description>Serializes the parameter dictionary to JSON and sets it as the request content.</description>
        ///   </item>
        ///   <item>
        ///     <description>Calls <see cref="IHttpAuthProvider.ApplyAuthAsync"/> to inject authentication
        ///     information (e.g., Bearer Token).</description>
        ///   </item>
        ///   <item>
        ///     <description>Uses <see cref="CancellationTokenSource.CreateLinkedTokenSource"/> to link
        ///     the incoming cancellation token with a timeout token, ensuring the request is aborted
        ///     when either the timeout elapses or cancellation is requested.</description>
        ///   </item>
        ///   <item>
        ///     <description>Sends the request using the static shared <see cref="HttpClient"/> and calls
        ///     <c>EnsureSuccessStatusCode</c> to validate the response status.</description>
        ///   </item>
        ///   <item>
        ///     <description>Reads the response content as a string and deserializes it into the target
        ///     type <typeparamref name="T"/> using <paramref name="ti"/> or reflection.</description>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <typeparam name="T">The deserialization target type for the response data.</typeparam>
        /// <param name="url">The target URL for the request.</param>
        /// <param name="p">The POST body parameter dictionary.</param>
        /// <param name="ti">The JSON type info metadata for source generator (may be null).</param>
        /// <param name="t">A <see cref="CancellationToken"/> for cancelling the operation.</param>
        /// <returns>The deserialized response data.</returns>
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

        /// <summary>
        /// Determines whether an exception is retryable.
        /// </summary>
        /// <param name="ex">The exception to evaluate.</param>
        /// <returns><c>true</c> if the exception is retryable; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// Retryable exceptions:
        /// <list type="bullet">
        ///   <item><description><see cref="TaskCanceledException"/></description></item>
        ///   <item><description><see cref="TimeoutException"/></description></item>
        ///   <item><description><see cref="System.IO.IOException"/></description></item>
        ///   <item><description><see cref="HttpRequestException"/> with a message containing "timeout"</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Non-retryable exceptions:
        /// <list type="bullet">
        ///   <item><description><see cref="OperationCanceledException"/></description></item>
        /// </list>
        /// </para>
        /// </remarks>
        private static bool IsRetryable(Exception ex)
        {
            if (ex is OperationCanceledException) return false;
            if (ex is TaskCanceledException or TimeoutException or System.IO.IOException) return true;
            if (ex is HttpRequestException h && (h.Message ?? "").Contains("timeout", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
