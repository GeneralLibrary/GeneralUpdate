using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>Reports update lifecycle events to the server, compatible with GeneralSpacestation API.</summary>
public interface IUpdateReporter
{
    Task ReportAsync(UpdateReport report, CancellationToken token = default);
}

/// <summary>Update event types mapped to Spacestation status codes.</summary>
public enum UpdateEvent { UpdateStarted = 1, DownloadCompleted = 1, UpdateApplied = 2, UpdateFailed = 3, AppStarted = 2 }

/// <summary>Spacestation-compatible update report: recordId from verification, status (1=updating,2=success,3=failure), type (1=upgrade,2=push).</summary>
public record UpdateReport(int RecordId, int Status = 1, int Type = 1);

/// <summary>HTTP POST reporter that serializes UpdateReport as JSON matching Spacestation ReportDTO.</summary>
public class HttpUpdateReporter : IUpdateReporter
{
    private readonly HttpClient _client;
    private readonly string _reportUrl;

    public HttpUpdateReporter(HttpClient client, string reportUrl)
    {
        _client = client;
        _reportUrl = reportUrl;
    }

    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var request = new HttpRequestMessage(HttpMethod.Post, _reportUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await _client.SendAsync(request, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report failed: {ex.Message}");
        }
    }
}

/// <summary>No-op reporter used when ReportUrl is not configured.</summary>
public class NoOpUpdateReporter : IUpdateReporter
{
    public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        => Task.CompletedTask;
}
