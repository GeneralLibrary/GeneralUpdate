using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Common.Shared.Security;

/// <summary>
/// HTTP request authentication provider.
/// Implement this interface to attach authentication credentials to outgoing HTTP requests.
/// The provider is invoked before every HTTP request made by the update framework
/// (version validation, download, status reporting).
/// </summary>
public interface IHttpAuthProvider
{
    /// <summary>
    /// Apply authentication to the given HTTP request message.
    /// Called immediately before the request is sent.
    /// </summary>
    /// <param name="request">The outgoing HTTP request to attach auth to.</param>
    /// <param name="token">Cancellation token.</param>
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default);
}

// ════════════════════════════════════════════════════════════════
// Built-in implementations
// ════════════════════════════════════════════════════════════════

/// <summary>
/// No-op auth provider. Used when no authentication is configured (the default).
/// </summary>
public sealed class NoOpAuthProvider : IHttpAuthProvider
{
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
/// Bearer token authentication (JWT / OAuth2).
/// Adds <c>Authorization: Bearer {token}</c> header.
/// </summary>
public sealed class BearerTokenAuthProvider : IHttpAuthProvider
{
    private readonly string _token;

    public BearerTokenAuthProvider(string token)
    {
        _token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
}

/// <summary>
/// API key authentication.
/// Adds a custom header (default <c>X-Api-Key</c>) with the provided API key.
/// </summary>
public sealed class ApiKeyAuthProvider : IHttpAuthProvider
{
    private readonly string _headerName;
    private readonly string _apiKey;

    public ApiKeyAuthProvider(string apiKey, string headerName = "X-Api-Key")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _headerName = headerName ?? throw new ArgumentNullException(nameof(headerName));
    }

    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Add(_headerName, _apiKey);
        return Task.CompletedTask;
    }
}

/// <summary>
/// HMAC-SHA256 signature authentication.
/// Adds <c>X-Update-Timestamp</c> and <c>X-Update-Signature</c> headers.
/// The signature is computed over <c>{request_body}|{unix_timestamp}</c>.
/// </summary>
public sealed class HmacAuthProvider : IHttpAuthProvider
{
    private readonly string _secretKey;

    public HmacAuthProvider(string secretKey)
    {
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync().ConfigureAwait(false)
            : string.Empty;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{body}|{timestamp}";
        var signature = ComputeHmacSha256(payload, _secretKey);

        request.Headers.Add("X-Update-Timestamp", timestamp);
        request.Headers.Add("X-Update-Signature", signature);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

/// <summary>
/// Factory methods for creating auth providers from UpdateOptions-style parameters.
/// Used internally by VersionService to auto-select the correct provider.
/// </summary>
public static class HttpAuthProviderFactory
{
    /// <summary>
    /// Auto-select an auth provider based on the provided parameters.
    /// </summary>
    /// <param name="scheme">Auth scheme: "Bearer", "ApiKey", or null.</param>
    /// <param name="token">Auth token / API key.</param>
    /// <param name="appSecretKey">HMAC secret key (takes priority over Token/Scheme).</param>
    /// <returns>The appropriate IHttpAuthProvider.</returns>
    public static IHttpAuthProvider Create(string? scheme, string? token, string? appSecretKey)
    {
        // HMAC takes priority (used for signed requests)
        if (!string.IsNullOrEmpty(appSecretKey))
            return new HmacAuthProvider(appSecretKey);

        // Bearer / API Key based on scheme
        if (!string.IsNullOrEmpty(token))
        {
            return (scheme ?? string.Empty).ToLowerInvariant() switch
            {
                "apikey" => new ApiKeyAuthProvider(token),
                _ => new BearerTokenAuthProvider(token) // Bearer is the default
            };
        }

        return new NoOpAuthProvider();
    }
}
