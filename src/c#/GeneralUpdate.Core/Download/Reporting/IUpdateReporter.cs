using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>
/// Defines a contract for reporting update lifecycle events to a remote server
/// compatible with the GeneralSpacestation API.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible for sending update status changes to a remote service
/// so that the update progress and outcome can be tracked centrally.
/// Typical update statuses include:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="UpdateStatus.Updating"/> — The update is in progress.</description></item>
///   <item><description><see cref="UpdateStatus.Success"/> — The update completed successfully.</description></item>
///   <item><description><see cref="UpdateStatus.Failure"/> — The update failed.</description></item>
/// </list>
/// <para>
/// The default no-op implementation <see cref="NoOpUpdateReporter"/> is used when no ReportUrl is configured.
/// The standard implementation <see cref="HttpUpdateReporter"/> sends status via HTTP POST to a configured endpoint.
/// </para>
/// </remarks>
public interface IUpdateReporter
{
    /// <summary>
    /// Asynchronously reports the update status to the remote server.
    /// </summary>
    /// <param name="report">The <see cref="UpdateReport"/> containing the record ID, status code, and type.</param>
    /// <param name="token">A <see cref="CancellationToken"/> to cancel the report operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReportAsync(UpdateReport report, CancellationToken token = default);
}

/// <summary>
/// Enumerates update status values that match the GeneralSpacestation API ReportDTO contract.
/// </summary>
/// <remarks>
/// Maps to the following integer codes:
/// <list type="bullet">
///   <item><description>1 = <see cref="UpdateStatus.Updating"/> — The update is currently in progress.</description></item>
///   <item><description>2 = <see cref="UpdateStatus.Success"/> — The update completed successfully.</description></item>
///   <item><description>3 = <see cref="UpdateStatus.Failure"/> — The update failed.</description></item>
/// </list>
/// </remarks>
public enum UpdateStatus { Updating = 1, Success = 2, Failure = 3 }

/// <summary>
/// Represents an update report record compatible with the GeneralSpacestation API.
/// Contains the record ID returned from version validation, the status code, and the update type.
/// </summary>
/// <param name="RecordId">The record ID returned from version validation, used to identify this update record.</param>
/// <param name="Status">The update status code: 1 = Updating, 2 = Success, 3 = Failure. Defaults to 1 (Updating).</param>
/// <param name="Type">The update type: 1 = Upgrade, 2 = Push. Defaults to 1 (Upgrade).</param>
/// <remarks>
/// This record is serialized to JSON using camelCase naming policy.
/// Example JSON: {"recordId": 123, "status": 1, "type": 1}
/// </remarks>
public record UpdateReport(int RecordId, int Status = 1, int Type = 1);

/// <summary>
/// An HTTP POST-based update status reporter that serializes <see cref="UpdateReport"/> to JSON
/// and sends it to a configured remote endpoint. Compatible with the GeneralSpacestation ReportDTO format.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IUpdateReporter"/> and provides the standard HTTP implementation
/// for reporting update status to a remote server.
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Serializes the <see cref="UpdateReport"/> to a JSON string using camelCase naming policy.</item>
///   <item>Creates an HTTP POST request with Content-Type set to application/json.</item>
///   <item>Sends the request to the configured report URL.</item>
///   <item>If the request fails (e.g., network error), logs a warning without throwing an exception,
///         to avoid disrupting the main update flow.</item>
/// </list>
/// </para>
/// </remarks>
public class HttpUpdateReporter : IUpdateReporter
{
    private HttpClient _client;
    private string _reportUrl;

    /// <summary>
    /// Gets or sets the report URL for update status reporting.
    /// When null or empty, <see cref="ReportAsync"/> is a no-op.
    /// </summary>
    public string ReportUrl
    {
        get => _reportUrl;
        set => _reportUrl = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the <see cref="HttpClient"/> used for HTTP requests.
    /// If not set, a default instance is created in the parameterless constructor.
    /// </summary>
    public HttpClient Client
    {
        get => _client;
        set => _client = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Parameterless constructor required by the extension resolution mechanism.
    /// Uses a default HttpClient and empty ReportUrl (no-op until configured).
    /// </summary>
    public HttpUpdateReporter()
    {
        _client = new HttpClient();
        _reportUrl = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpUpdateReporter"/> class
    /// with a specific client and report URL.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance used to send HTTP requests.</param>
    /// <param name="reportUrl">The remote URL that receives the update status reports.</param>
    public HttpUpdateReporter(HttpClient client, string reportUrl)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _reportUrl = reportUrl ?? string.Empty;
    }

    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(_reportUrl))
                return;
            
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

/// <summary>
/// A no-op (null object) update status reporter that performs no actual work.
/// Used when no ReportUrl is configured.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the Null Object Pattern, eliminating the need for null checks
/// in consumer code. Every report operation returns a completed task immediately without
/// performing any actual data transmission.
/// </para>
/// <para>
/// Use this implementation when no remote status reporting is needed,
/// such as during local testing or when the report endpoint is not configured.
/// </para>
/// </remarks>
public class NoOpUpdateReporter : IUpdateReporter
{
    public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        => Task.CompletedTask;
}
