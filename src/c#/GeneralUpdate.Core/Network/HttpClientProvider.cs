using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network;

/// <summary>
/// Provides a shared static <see cref="HttpClient"/> instance with configurable SSL validation.
/// Reusing a single HttpClient prevents socket exhaustion.
/// Do NOT dispose clients obtained from here.
/// </summary>
/// <remarks>
/// <para>
/// SSL certificate validation behaviour is controlled by the
/// <see cref="SetSslValidationPolicy"/> method. The default policy is
/// <see cref="StrictSslValidationPolicy"/> (rejects any certificate with SSL errors).
/// </para>
/// <para>
/// This class mirrors the SSL validation pattern used by <see cref="VersionService"/>,
/// ensuring that both API calls and file downloads respect the same global SSL policy.
/// </para>
/// </remarks>
public static class HttpClientProvider
{
    private static ISslValidationPolicy _sslPolicy = new StrictSslValidationPolicy();
    private static readonly HttpClient _shared;

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
}
