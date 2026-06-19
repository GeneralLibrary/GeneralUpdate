using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network;

/// <summary>
/// Provides a shared static <see cref="HttpClient"/> instance with configurable SSL validation
/// and a global HTTP authentication provider. Reusing a single HttpClient prevents socket
/// exhaustion. Do NOT dispose clients obtained from here.
/// </summary>
/// <remarks>
/// <para>
/// SSL certificate validation behaviour is controlled by the
/// <see cref="SetSslValidationPolicy"/> method. The default policy is
/// <see cref="StrictSslValidationPolicy"/> (rejects any certificate with SSL errors).
/// </para>
/// <para>
/// The global <see cref="IHttpAuthProvider"/> set via <see cref="DefaultAuthProvider"/> is
/// applied by <see cref="ApplyAuthAsync"/> so that components like <see cref="HttpUpdateReporter"/>
/// participate in the same authentication scheme as <see cref="VersionService"/>.
/// Extra headers set via <see cref="ExtraHeaders"/> are applied after the auth provider.
/// </para>
/// </remarks>
public static class HttpClientProvider
{
    private static ISslValidationPolicy _sslPolicy = new StrictSslValidationPolicy();
    private static IHttpAuthProvider? _defaultAuthProvider;
    private static readonly HttpClient _shared;

    // Headers that belong on HttpContentMessage.Headers, not HttpRequestHeaders.
    // Setting them via request.Headers throws InvalidOperationException.
    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Length", "Content-Encoding", "Content-Language",
        "Content-Location", "Content-Disposition", "Content-Range", "Content-MD5",
        "Last-Modified", "Expires", "Allow"
    };

    static HttpClientProvider()
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
            _sslPolicy.ValidateCertificate(cert, chain, errors);
        _shared = new HttpClient(handler, disposeHandler: false)
        {
            // Default 5-minute hard upper bound as a safety net. Per-request
            // CancellationTokenSource.CancelAfter (set by HttpDownloadExecutor
            // and VersionService) provides the primary timeout — this value
            // only catches leaked requests where no per-request timeout was set.
            // Callers requiring transfers longer than 5 minutes must increase
            // this timeout or pass a larger value via e.g. UpdateRequest.
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Sets the global SSL certificate validation policy for all download requests.
    /// </summary>
    /// <param name="policy">The SSL validation policy instance. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This policy affects all HTTPS download requests made through
    /// <see cref="HttpDownloadExecutor"/> and other components that use the shared
    /// <see cref="HttpClient"/>. The default is <see cref="StrictSslValidationPolicy"/>.
    /// </para>
    /// <para>
    /// The validation callback delegates to the stored policy instance, so the policy
    /// can be changed at any time — new requests will use the updated policy.
    /// </para>
    /// </remarks>
    public static void SetSslValidationPolicy(ISslValidationPolicy policy)
        => _sslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));

    /// <summary>
    /// Gets the shared <see cref="HttpClient"/> instance. Do NOT dispose.
    /// </summary>
    public static HttpClient Shared => _shared;

    /// <summary>
    /// Gets or sets the global default HTTP authentication provider.
    /// When set, every HTTP call from <see cref="VersionService"/>,
    /// <see cref="HttpUpdateReporter"/>, and other components using
    /// <see cref="ApplyAuthAsync"/> will carry the configured credentials.
    /// </summary>
    public static IHttpAuthProvider? DefaultAuthProvider
    {
        get => _defaultAuthProvider;
        set => _defaultAuthProvider = value;
    }

    /// <summary>
    /// Extra HTTP headers applied to every outgoing request by <see cref="ApplyAuthAsync"/>.
    /// Use this for headers that are not part of the authentication scheme itself but
    /// are required by the target server — for example <c>X-Tenant-Id</c> for tenant scoping.
    /// </summary>
    /// <remarks>
    /// The key is the header name, the value is the header value.
    /// Existing headers of the same name on the request are overwritten.
    /// This dictionary is thread-safe.
    /// </remarks>
    public static ConcurrentDictionary<string, string> ExtraHeaders { get; }
        = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Applies the global default authentication provider and extra headers
    /// to the specified request. When no global provider is configured,
    /// the auth step is a no-op.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extra headers (see <see cref="ExtraHeaders"/>) are applied after the auth provider,
    /// so they can supplement authentication headers and also appear on their own when
    /// no auth provider is configured. If an extra header key matches a content header
    /// (e.g. <c>Content-Type</c>), it is set on <c>request.Content.Headers</c> instead
    /// of <c>request.Headers</c> to avoid <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    /// <param name="request">The HTTP request message to authenticate.</param>
    /// <param name="token">A cancellation token for the authentication operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        if (_defaultAuthProvider != null)
            await _defaultAuthProvider.ApplyAuthAsync(request, token).ConfigureAwait(false);

        foreach (var kv in ExtraHeaders)
        {
            if (ContentHeaderNames.Contains(kv.Key) && request.Content != null)
            {
                request.Content.Headers.Remove(kv.Key);
                request.Content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
            else
            {
                request.Headers.Remove(kv.Key);
                request.Headers.Add(kv.Key, kv.Value);
            }
        }
    }
}
