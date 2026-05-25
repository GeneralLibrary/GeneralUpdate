using System.Net.Http;

namespace GeneralUpdate.Core.Network;

/// <summary>
/// Provides a shared static <see cref="HttpClient"/> instance.
/// Reusing a single HttpClient prevents socket exhaustion.
/// Do NOT dispose clients obtained from here.
/// </summary>
public static class HttpClientProvider
{
    private static readonly HttpClient _shared = new();

    /// <summary>Shared <see cref="HttpClient"/> instance. Do NOT dispose.</summary>
    public static HttpClient Shared => _shared;
}
