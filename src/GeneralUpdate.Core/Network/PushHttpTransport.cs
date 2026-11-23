using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network;

internal static class PushHttpTransport
{
    internal static readonly HttpClient Client = HttpClientProvider.CreateClient(allowAutoRedirect: false);

    internal static async Task ApplyAuthAsync(
        UpdateContext config, HttpRequestMessage request, CancellationToken token)
    {
        var provider = HttpAuthProviderFactory.Create(config.Scheme, config.Token, config.AppSecretKey,
            config.AuthScheme, config.BasicUsername, config.BasicPassword);
        await provider.ApplyAuthAsync(request, token).ConfigureAwait(false);
        ApplyHeaders(config, request);
    }

    internal static void ApplyHeaders(UpdateContext config, HttpRequestMessage request)
    {
        if (config.CustomHeaders == null) return;
        foreach (var header in config.CustomHeaders)
        {
            request.Headers.Remove(header.Key);
            request.Headers.Add(header.Key, header.Value);
        }
    }
}
