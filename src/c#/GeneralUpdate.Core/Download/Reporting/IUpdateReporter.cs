using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>Reports update lifecycle events to the server.</summary>
public interface IUpdateReporter
{
    Task ReportAsync(UpdateReport report, CancellationToken token = default);
}

public enum UpdateEvent { UpdateStarted, DownloadCompleted, UpdateApplied, UpdateFailed, AppStarted }

public record UpdateReport(
    string AppName,
    string FromVersion,
    string? ToVersion,
    UpdateEvent Event,
    int AppType,
    DateTimeOffset Timestamp,
    string? ErrorMessage = null,
    double? DurationMs = null
);

/// <summary>HTTP POST reporter with optional HMAC signing.</summary>
public class HttpUpdateReporter : IUpdateReporter
{
    private readonly HttpClient _client;
    private readonly string _reportUrl;
    private readonly string? _secretKey;

    public HttpUpdateReporter(HttpClient client, string reportUrl, string? secretKey = null)
    {
        _client = client;
        _reportUrl = reportUrl;
        _secretKey = secretKey;
    }

    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(report);

            using var request = new HttpRequestMessage(HttpMethod.Post, _reportUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(_secretKey))
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var sig = ComputeHmac($"{json}|{ts}", _secretKey);
                request.Headers.Add("X-Update-Timestamp", ts);
                request.Headers.Add("X-Update-Signature", sig);
            }

            await _client.SendAsync(request, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Silent failure — reporting should never break the update flow
            GeneralTracer.Warn($"Report failed: {ex.Message}");
        }
    }

    private static string ComputeHmac(string data, string key)
    {
        var h = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(key))
            .ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
    }
}

/// <summary>No-op reporter used when ReportUrl is not configured.</summary>
public class NoOpUpdateReporter : IUpdateReporter
{
    public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        => Task.CompletedTask;
}
