using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Security;

/// <summary>
/// Explicitly selects the HTTP authentication method for update requests.
/// </summary>
/// <remarks>
/// Use this enum to tell the framework which authentication scheme to use,
/// then supply the corresponding credentials:
/// <list type="bullet">
///   <item><description><see cref="Hmac"/> — uses <c>AppSecretKey</c> (default).</description></item>
///   <item><description><see cref="Bearer"/> — uses <c>Token</c>.</description></item>
///   <item><description><see cref="ApiKey"/> — uses <c>Token</c>.</description></item>
///   <item><description><see cref="Basic"/> — uses <c>BasicUsername</c> + <c>BasicPassword</c>.</description></item>
/// </list>
/// </remarks>
public enum AuthScheme
{
    /// <summary>HMAC-SHA256 signature authentication (default). Requires <c>AppSecretKey</c>.</summary>
    Hmac,

    /// <summary>Bearer Token authentication. Requires <c>Token</c>.</summary>
    Bearer,

    /// <summary>API Key authentication via custom header. Requires <c>Token</c>.</summary>
    ApiKey,

    /// <summary>HTTP Basic Authentication (RFC 7617). Requires <c>BasicUsername</c> + <c>BasicPassword</c>.</summary>
    Basic
}

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
///   <item><description><see cref="BasicAuthProvider"/> — sets the <c>Authorization: Basic</c> header with a Base64-encoded credential.</description></item>
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
/// An authentication provider that uses the HTTP Basic Authentication scheme
/// (<c>Authorization: Basic &lt;credential&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// This provider sets the <c>Authorization</c> header to <c>Basic &lt;credential&gt;</c>
/// where <c>credential</c> is the Base64-encoded string of <c>username:password</c>.
/// Use <see cref="EncodeCredential"/> to generate the encoded value from a username and password pair.
/// </para>
/// <para>
/// This scheme is defined in RFC 7617 and is widely used by enterprise artifact
/// repositories (Nexus, JFrog Artifactory), internal file servers, and simple API gateways.
/// </para>
/// </remarks>
public sealed class BasicAuthProvider : IHttpAuthProvider
{
    private readonly string _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthProvider"/> class.
    /// </summary>
    /// <param name="base64Credential">
    /// The Base64-encoded credential string (i.e., the result of Base64-encoding <c>username:password</c>).
    /// Must not be null. Use <see cref="EncodeCredential"/> to generate this value safely.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="base64Credential"/> is null.</exception>
    public BasicAuthProvider(string base64Credential)
        => _credential = base64Credential ?? throw new ArgumentNullException(nameof(base64Credential));

    /// <summary>
    /// Encodes a username and password pair into a Base64 credential string suitable for
    /// use with the HTTP Basic Authentication scheme.
    /// </summary>
    /// <param name="username">The username. Must not be null or empty.</param>
    /// <param name="password">The password. Must not be null or empty.</param>
    /// <returns>The Base64-encoded credential string for use with <c>Authorization: Basic</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> or <paramref name="password"/> is null, empty, or whitespace.</exception>
    public static string EncodeCredential(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username must not be null or empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password must not be null or empty.", nameof(password));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    /// <summary>
    /// Applies the Basic authentication by setting the <c>Authorization</c> header.
    /// </summary>
    /// <param name="request">The HTTP request message to authenticate.</param>
    /// <param name="token">A <see cref="CancellationToken"/> (ignored by this implementation).</param>
    /// <returns>A completed task after setting the header.</returns>
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credential);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Factory class for creating <see cref="IHttpAuthProvider"/> instances based on a scheme string and token.
/// </summary>
/// <remarks>
/// <para>
/// This factory encapsulates the logic for selecting the appropriate authentication provider
/// based on the authentication scheme name. Explicit scheme+credentials take priority:
/// <list type="bullet">
///   <item><description>If <paramref name="scheme"/> is <c>"basic"</c> with <paramref name="basicUsername"/>/<paramref name="basicPassword"/>,
///   creates a <see cref="BasicAuthProvider"/> (auto-encodes credentials).</description></item>
///   <item><description>If <paramref name="scheme"/> is <c>"basic"</c> with <paramref name="token"/>,
///   creates a <see cref="BasicAuthProvider"/> (pre-encoded credential).</description></item>
///   <item><description>If <paramref name="scheme"/> is <c>"apikey"</c> with <paramref name="token"/>,
///   creates an <see cref="ApiKeyAuthProvider"/>.</description></item>
///   <item><description>If <paramref name="token"/> is provided with any other scheme,
///   creates a <see cref="BearerTokenAuthProvider"/>.</description></item>
///   <item><description>If only <paramref name="secretKey"/> is provided (no explicit scheme),
///   creates an <see cref="HmacAuthProvider"/> as the default.</description></item>
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
    /// <param name="scheme">Legacy string-based scheme. Prefer using <paramref name="authScheme"/> instead.</param>
    /// <param name="token">The authentication token or API key value.</param>
    /// <param name="secretKey">The HMAC secret key. Used as the default authentication when no explicit scheme is specified.</param>
    /// <param name="authScheme">Explicitly selects the HTTP authentication method. Defaults to <see cref="AuthScheme.Hmac"/>.</param>
    /// <param name="basicUsername">The username for HTTP Basic Authentication. Used together with <paramref name="basicPassword"/> when <paramref name="scheme"/> is "basic".</param>
    /// <param name="basicPassword">The password for HTTP Basic Authentication. Used together with <paramref name="basicUsername"/> when <paramref name="scheme"/> is "basic".</param>
    /// <returns>An <see cref="IHttpAuthProvider"/> instance matching the specified parameters,
    /// or <see cref="NoOpAuthProvider"/> if no matching scheme is found.</returns>
    public static IHttpAuthProvider Create(string? scheme, string? token, string? secretKey, AuthScheme authScheme = AuthScheme.Hmac, string? basicUsername = null, string? basicPassword = null)
    {
        // ── Explicit enum selection (preferred) ──
        switch (authScheme)
        {
            case AuthScheme.Basic:
                if (!string.IsNullOrWhiteSpace(basicUsername) && !string.IsNullOrWhiteSpace(basicPassword))
                    return new BasicAuthProvider(BasicAuthProvider.EncodeCredential(basicUsername, basicPassword));
                if (!string.IsNullOrEmpty(token))
                    return new BasicAuthProvider(token);
                break;

            case AuthScheme.ApiKey:
                if (!string.IsNullOrEmpty(token))
                    return new ApiKeyAuthProvider(token);
                break;

            case AuthScheme.Bearer:
                if (!string.IsNullOrEmpty(token))
                    return new BearerTokenAuthProvider(token);
                break;

            case AuthScheme.Hmac:
            default:
                // Default: HMAC when secretKey is provided
                if (!string.IsNullOrEmpty(secretKey))
                    return new HmacAuthProvider(secretKey);
                break;
        }

        // ── Legacy fallback: string-based scheme ──
        var s = (scheme ?? "").ToLowerInvariant();
        if (s == "basic" && !string.IsNullOrWhiteSpace(basicUsername) && !string.IsNullOrWhiteSpace(basicPassword))
            return new BasicAuthProvider(BasicAuthProvider.EncodeCredential(basicUsername, basicPassword));
        if (s == "basic" && !string.IsNullOrEmpty(token))
            return new BasicAuthProvider(token);
        if (s == "apikey" && !string.IsNullOrEmpty(token))
            return new ApiKeyAuthProvider(token);
        if (!string.IsNullOrEmpty(token) && s != "basic" && s != "apikey")
            return new BearerTokenAuthProvider(token);

        return new NoOpAuthProvider();
    }
}
