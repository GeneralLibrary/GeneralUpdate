using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Security;

/// <summary>
/// Defines a strategy for applying HTTP authentication to outgoing requests.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface inject authentication credentials into
/// <see cref="HttpRequestMessage"/> instances before they are sent to the server.
/// The authentication mechanism is abstracted so that different schemes (Bearer token,
/// API key, HMAC signature, etc.) can be used interchangeably.
/// </para>
/// <para>
/// Built-in implementations:
/// <list type="bullet">
///   <item><description><see cref="NoOpAuthProvider"/> — performs no authentication.</description></item>
///   <item><description><see cref="BearerTokenAuthProvider"/> — sets the <c>Authorization: Bearer</c> header.</description></item>
///   <item><description><see cref="ApiKeyAuthProvider"/> — sets a custom header with the API key value.</description></item>
///   <item><description><see cref="HmacAuthProvider"/> — computes an HMAC-SHA256 signature over the request body and timestamp.</description></item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="HttpAuthProviderFactory.Create"/> to instantiate the appropriate provider
/// based on a scheme string and token, or set a global default via
/// <see cref="Network.VersionService.SetDefaultAuthProvider(IHttpAuthProvider)"/>.
/// </para>
/// </remarks>
public interface IHttpAuthProvider
{
    /// <summary>
    /// Applies authentication credentials to the specified HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request message to authenticate. Headers and properties may be modified.</param>
    /// <param name="token">A <see cref="CancellationToken"/> for cancelling the authentication operation.</param>
    /// <returns>A task representing the asynchronous authentication operation.</returns>
    /// <remarks>
    /// Implementations should modify <paramref name="request"/> in place by adding or modifying
    /// headers. The method is asynchronous to support schemes that require computation or
    /// external lookups (e.g., HMAC signature generation over request content).
    /// </remarks>
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default);
}

/// <summary>
/// An authentication provider that performs no authentication.
/// Used as the default when no authentication scheme is configured.
/// </summary>
/// <remarks>
/// This provider simply returns a completed task without modifying the request.
/// It is suitable for public APIs or when authentication is handled by other means
/// (e.g., network-level authentication, reverse proxy).
/// </remarks>
public sealed class NoOpAuthProvider : IHttpAuthProvider
{
    /// <summary>
    /// Applies no authentication. Returns immediately without modifying the request.
    /// </summary>
    /// <param name="request">The HTTP request message (unmodified).</param>
    /// <param name="token">A <see cref="CancellationToken"/> (ignored by this implementation).</param>
    /// <returns>A completed task.</returns>
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
/// An authentication provider that uses the Bearer Token scheme
/// (<c>Authorization: Bearer &lt;token&gt;</c>).
/// </summary>
/// <remarks>
/// This provider sets the <c>Authorization</c> header to <c>Bearer &lt;token&gt;</c> on the
/// outgoing request. This is one of the most common authentication schemes for REST APIs.
/// </remarks>
public sealed class BearerTokenAuthProvider : IHttpAuthProvider
{
    private readonly string _token;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthProvider"/> class.
    /// </summary>
    /// <param name="token">The Bearer token value. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
    public BearerTokenAuthProvider(string token)
        => _token = token ?? throw new ArgumentNullException(nameof(token));

    /// <summary>
    /// Applies the Bearer token authentication by setting the <c>Authorization</c> header.
    /// </summary>
    /// <param name="request">The HTTP request message to authenticate.</param>
    /// <param name="token">A <see cref="CancellationToken"/> (ignored by this implementation).</param>
    /// <returns>A completed task after setting the header.</returns>
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An authentication provider that sends an API key via a custom HTTP header.
/// </summary>
/// <remarks>
/// The default header name is <c>X-Api-Key</c>, but a custom header name can be specified
/// in the constructor. This scheme is commonly used by API gateways and managed API services.
/// </remarks>
public sealed class ApiKeyAuthProvider : IHttpAuthProvider
{
    private readonly string _h;
    private readonly string _k;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The API key value. Must not be null.</param>
    /// <param name="headerName">The HTTP header name to use. Defaults to <c>X-Api-Key</c>. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="apiKey"/> or <paramref name="headerName"/> is null.</exception>
    public ApiKeyAuthProvider(string apiKey, string headerName = "X-Api-Key")
    {
        _k = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _h = headerName ?? throw new ArgumentNullException(nameof(headerName));
    }

    /// <summary>
    /// Applies the API key authentication by adding the configured header to the request.
    /// </summary>
    /// <param name="request">The HTTP request message to authenticate.</param>
    /// <param name="token">A <see cref="CancellationToken"/> (ignored by this implementation).</param>
    /// <returns>A completed task after adding the header.</returns>
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Add(_h, _k);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An authentication provider that computes an HMAC-SHA256 signature over the
/// request body and current timestamp.
/// </summary>
/// <remarks>
/// <para>
/// This provider implements a request-signing authentication scheme. It adds two headers:
/// <list type="bullet">
///   <item><description><c>X-Update-Timestamp</c>: the current Unix timestamp in seconds.</description></item>
///   <item><description><c>X-Update-Signature</c>: an HMAC-SHA256 hash of the concatenation
///   <c>body|timestamp</c> using the configured secret key.</description></item>
/// </list>
/// </para>
/// <para>
/// The receiving server recomputes the signature using the same secret to verify the
/// request's authenticity and integrity, and can optionally reject requests with stale
/// timestamps to prevent replay attacks.
/// </para>
/// </remarks>
public sealed class HmacAuthProvider : IHttpAuthProvider
{
    private readonly string _secret;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacAuthProvider"/> class.
    /// </summary>
    /// <param name="secretKey">The shared secret key used for HMAC signing. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secretKey"/> is null.</exception>
    public HmacAuthProvider(string secretKey)
        => _secret = secretKey ?? throw new ArgumentNullException(nameof(secretKey));

    /// <summary>
    /// Applies HMAC authentication by computing a signature over the request body and timestamp.
    /// </summary>
    /// <param name="request">The HTTP request message to authenticate.</param>
    /// <param name="token">A <see cref="CancellationToken"/> for cancelling the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    ///   <item><description>Reads the request body content as a string (or empty string if no content).</description></item>
    ///   <item><description>Gets the current UTC Unix timestamp as a string.</description></item>
    ///   <item><description>Computes an HMAC-SHA256 hash of the concatenated string <c>body|timestamp</c>
    ///   using the configured secret key.</description></item>
    ///   <item><description>Sets the <c>X-Update-Timestamp</c> and <c>X-Update-Signature</c> headers on the request.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig = HmacSha256($"{body}|{ts}", _secret);
        request.Headers.Add("X-Update-Timestamp", ts);
        request.Headers.Add("X-Update-Signature", sig);
    }

    /// <summary>
    /// Computes an HMAC-SHA256 hash of the input data using the specified key.
    /// </summary>
    /// <param name="data">The input data string to hash.</param>
    /// <param name="key">The HMAC secret key.</param>
    /// <returns>The HMAC-SHA256 hash as a lowercase hexadecimal string.</returns>
    private static string HmacSha256(string data, string key)
    {
        var h = new HMACSHA256(Encoding.UTF8.GetBytes(key))
            .ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
    }
}

/// <summary>
/// Factory class for creating <see cref="IHttpAuthProvider"/> instances based on a scheme string and token.
/// </summary>
/// <remarks>
/// <para>
/// This factory encapsulates the logic for selecting the appropriate authentication provider
/// based on the authentication scheme name:
/// <list type="bullet">
///   <item><description>If <paramref name="secretKey"/> is provided, creates an <see cref="HmacAuthProvider"/>.</description></item>
///   <item><description>If <paramref name="scheme"/> is <c>"apikey"</c>, creates an <see cref="ApiKeyAuthProvider"/>.</description></item>
///   <item><description>If <paramref name="token"/> is provided (any other scheme), creates a <see cref="BearerTokenAuthProvider"/>.</description></item>
///   <item><description>Otherwise, returns a <see cref="NoOpAuthProvider"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// The factory is used internally by <see cref="Network.VersionService"/> when no global
/// authentication provider has been set via <see cref="Network.VersionService.SetDefaultAuthProvider(IHttpAuthProvider)"/>.
/// </para>
/// </remarks>
public static class HttpAuthProviderFactory
{
    /// <summary>
    /// Creates an <see cref="IHttpAuthProvider"/> based on the provided scheme, token, and secret key.
    /// </summary>
    /// <param name="scheme">The authentication scheme name (e.g., "bearer", "apikey", "hmac"). Case-insensitive.</param>
    /// <param name="token">The authentication token or API key value.</param>
    /// <param name="secretKey">The HMAC secret key. If provided, HMAC authentication takes precedence over other schemes.</param>
    /// <returns>An <see cref="IHttpAuthProvider"/> instance matching the specified parameters,
    /// or <see cref="NoOpAuthProvider"/> if no matching scheme is found.</returns>
    public static IHttpAuthProvider Create(string? scheme, string? token, string? secretKey)
    {
        if (!string.IsNullOrEmpty(secretKey)) return new HmacAuthProvider(secretKey);
        if (!string.IsNullOrEmpty(token))
            return (scheme ?? "").ToLowerInvariant() switch
            {
                "apikey" => new ApiKeyAuthProvider(token),
                _ => new BearerTokenAuthProvider(token)
            };
        return new NoOpAuthProvider();
    }
}
