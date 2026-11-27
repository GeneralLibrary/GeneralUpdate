using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Network;

internal static class PushValidationClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static async Task<bool> ValidateAsync(
        UpdateContext config,
        int recordId,
        string? appKey,
        string? packetHash,
        CancellationToken cancellationToken = default)
    {
        if (recordId <= 0 || string.IsNullOrWhiteSpace(appKey) || !IsSha256(packetHash) ||
            !Uri.TryCreate(config.PushValidationUrl, UriKind.Absolute, out var endpoint) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            (endpoint.Scheme != Uri.UriSchemeHttps &&
             (endpoint.Scheme != Uri.UriSchemeHttp || !endpoint.IsLoopback)))
        {
            GeneralTracer.Warn("PushValidationClient: rejected incomplete push validation data.");
            return false;
        }

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(RequestTimeout);

        var body = new PushValidationRequest
        {
            RecordId = recordId,
            AppKey = appKey,
            PacketHash = packetHash!
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, PushPayloadJsonContext.Default.PushValidationRequest);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            PushHttpTransport.ApplyHeaders(config, request);
            using var response = await PushHttpTransport.Client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestTimeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                GeneralTracer.Warn($"PushValidationClient: server rejected validation with HTTP {(int)response.StatusCode}.");
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync(
                stream,
                PushPayloadJsonContext.Default.PushValidationResponse,
                requestTimeout.Token).ConfigureAwait(false);
            if (result?.Code != 200 || !result.Body)
            {
                GeneralTracer.Warn("PushValidationClient: server did not approve the push record.");
                return false;
            }

            return true;
        }
        catch (OperationCanceledException ex)
        {
            GeneralTracer.Warn($"PushValidationClient: validation timed out or was cancelled: {ex.Message}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            GeneralTracer.Warn($"PushValidationClient: validation request failed: {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            GeneralTracer.Warn($"PushValidationClient: server returned an invalid validation response: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            GeneralTracer.Warn($"PushValidationClient: failed to read validation response: {ex.Message}");
            return false;
        }
    }

    private static bool IsSha256(string? value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (var character in value)
            if (!Uri.IsHexDigit(character))
                return false;
        return true;
    }
}
