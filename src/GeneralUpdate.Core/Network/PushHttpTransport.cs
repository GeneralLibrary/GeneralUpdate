using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using System;

namespace GeneralUpdate.Core.Network;

internal static class PushHttpTransport
{
    internal static readonly HttpClient Client = HttpClientProvider.CreateClient(allowAutoRedirect: false);

    internal static Task ApplyAuthAsync(
        UpdateContext config, HttpRequestMessage request, CancellationToken token)
    {
        ApplyHeaders(config, request);
        return Task.CompletedTask;
    }

    internal static void ApplyHeaders(UpdateContext config, HttpRequestMessage request)
    {
        if (config.CustomHeaders?.Count > 0 || !string.IsNullOrEmpty(config.Token) ||
            !string.IsNullOrEmpty(config.BasicUsername) || !string.IsNullOrEmpty(config.BasicPassword))
            throw new InvalidOperationException(
                "NOT DEPLOYABLE: credentials and tenant headers require authenticated IPC before automatic push can send them.");
    }
}
