using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Security;

public interface IHttpAuthProvider
{
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default);
}

public sealed class NoOpAuthProvider : IHttpAuthProvider
{
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
        => Task.CompletedTask;
}

public sealed class BearerTokenAuthProvider : IHttpAuthProvider
{
    private readonly string _token;
    public BearerTokenAuthProvider(string token)
        => _token = token ?? throw new ArgumentNullException(nameof(token));
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
}

public sealed class ApiKeyAuthProvider : IHttpAuthProvider
{
    private readonly string _h;
    private readonly string _k;
    public ApiKeyAuthProvider(string apiKey, string headerName = "X-Api-Key")
    {
        _k = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _h = headerName ?? throw new ArgumentNullException(nameof(headerName));
    }
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.Headers.Add(_h, _k);
        return Task.CompletedTask;
    }
}

public sealed class HmacAuthProvider : IHttpAuthProvider
{
    private readonly string _secret;
    public HmacAuthProvider(string secretKey)
        => _secret = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig = HmacSha256($"{body}|{ts}", _secret);
        request.Headers.Add("X-Update-Timestamp", ts);
        request.Headers.Add("X-Update-Signature", sig);
    }
    private static string HmacSha256(string data, string key)
    {
        var h = new HMACSHA256(Encoding.UTF8.GetBytes(key))
            .ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
    }
}

public static class HttpAuthProviderFactory
{
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
