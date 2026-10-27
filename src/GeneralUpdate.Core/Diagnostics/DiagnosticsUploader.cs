using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Network;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Upload capability of the diagnostics channel. An interface so the scanner and the
///     rollback path can be exercised with a fake in tests; production always uses
///     <see cref="DiagnosticsUploader"/>.
/// </summary>
public interface IDiagnosticsUploader
{
    /// <summary><c>true</c> when the uploader has a usable endpoint (enabled + URL).</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads a bundle (archive + manifest). Returns <c>true</c> on a 2xx response.</summary>
    Task<bool> UploadAsync(DiagnosticsBundle bundle, CancellationToken ct);
}

/// <summary>
///     Uploads diagnostics bundles to the configured endpoint. Core is the ONLY network egress
///     of the diagnostics feature — the Bowl watchdog stays fully offline.
/// </summary>
/// <remarks>
///     <para>
///         Wire format: <c>multipart/form-data</c> per the GeneralSpace
///         <c>POST /Fault/UploadArtifact</c> contract — the zip archive as the <c>file</c> part
///         plus the <see cref="DiagnosticsManifest"/> fields as form fields. The <c>guid</c>
///         field is the server-side idempotency key: replaying the same bundle returns 2xx
///         without storing a duplicate, which makes the outbox "delete after 2xx" rule safe.
///         The same manifest is also embedded inside the archive as <c>manifest.json</c> so the
///         server can audit contents without trusting the form (privacy/audit requirement).
///     </para>
///     <para>
///         Failure handling follows the reporter convention: upload failures are logged and
///         reported to the caller (<c>false</c>), never thrown into the main update flow.
///         Outbox evidence stays on disk for the next scan when an upload fails.
///     </para>
/// </remarks>
public sealed class DiagnosticsUploader : IDiagnosticsUploader
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly DiagnosticsOptions _options;
    private readonly HttpClient _client;

    /// <summary>
    ///     <c>true</c> when the uploader has a usable endpoint (enabled + URL). Callers use this
    ///     to skip collection work entirely when the channel is off.
    /// </summary>
    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Url);

    /// <param name="options">Runtime options (endpoint, size cap, switch).</param>
    /// <param name="client">
    ///     HTTP client; defaults to <see cref="HttpClientProvider.Shared"/> (honours the global
    ///     SSL policy). Inject a custom one in tests.
    /// </param>
    public DiagnosticsUploader(DiagnosticsOptions options, HttpClient? client = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? HttpClientProvider.Shared;
    }

    /// <summary>
    ///     Uploads a bundle (archive + manifest) as multipart form data.
    ///     Deletes the temporary archive afterwards.
    /// </summary>
    /// <returns><c>true</c> when the server accepted the bundle with a 2xx response.</returns>
    public async Task<bool> UploadAsync(DiagnosticsBundle bundle, CancellationToken ct)
    {
        if (bundle == null) throw new ArgumentNullException(nameof(bundle));
        if (!IsConfigured)
        {
            GeneralTracer.Info("DiagnosticsUploader: channel not configured — upload skipped.");
            TryDelete(bundle.ZipPath);
            return false;
        }

        try
        {
            var manifest = bundle.Manifest ?? new DiagnosticsManifest();
            using var stream = File.OpenRead(bundle.ZipPath);
            using var content = BuildMultipartContent(manifest, stream);

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
            {
                Content = content
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(RequestTimeout);
            using var response = await _client.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                GeneralTracer.Info(
                    $"DiagnosticsUploader: bundle {manifest.Guid} ({manifest.Kind}, {manifest.Files.Count} file(s)) uploaded.");
                return true;
            }

            // Bounded, code-only failure record — response bodies are never read into logs.
            GeneralTracer.Warn($"DiagnosticsUploader: upload rejected with HTTP {(int)response.StatusCode}.");
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            GeneralTracer.Warn("DiagnosticsUploader: upload timed out.");
            return false;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"DiagnosticsUploader: upload failed: {ex.Message}");
            return false;
        }
        finally
        {
            TryDelete(bundle.ZipPath);
        }
    }

    /// <summary>
    ///     Builds the <c>/Fault/UploadArtifact</c> multipart body: the <c>file</c> part plus one
    ///     form field per manifest attribute. Optional attributes are omitted (not sent empty) —
    ///     the server treats them as "not provided".
    /// </summary>
    internal static MultipartFormDataContent BuildMultipartContent(DiagnosticsManifest manifest, Stream zipStream)
    {
        var guid = string.IsNullOrWhiteSpace(manifest.Guid)
            ? Guid.NewGuid().ToString("D") // Defensive: the server needs a guid; collection normally provides it.
            : manifest.Guid;

        var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(zipStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", $"{guid}.zip");

        // Required contract fields.
        content.Add(new StringContent(guid), "guid");
        content.Add(new StringContent(manifest.Kind ?? DiagnosticsKinds.Manual), "kind");
        content.Add(new StringContent(manifest.Schema.ToString()), "schema");

        // Optional attribution fields — present only when known.
        AddIfNotEmpty(content, "appKey", manifest.AppKey);
        AddIfNotEmpty(content, "version", manifest.Version);
        AddIfNotEmpty(content, "targetName", manifest.TargetName);
        AddIfNotEmpty(content, "productId", manifest.ProductId);
        AddIfNotEmpty(content, "reason", manifest.Reason);
        if (manifest.Platform.HasValue)
            content.Add(new StringContent(manifest.Platform.Value.ToString()), "platform");
        if (manifest.RecordId is > 0)
            content.Add(new StringContent(manifest.RecordId.Value.ToString()), "recordId");
        if (manifest.RecordType is > 0)
            content.Add(new StringContent(manifest.RecordType.Value.ToString()), "recordType");

        return content;
    }

    private static void AddIfNotEmpty(MultipartFormDataContent content, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            content.Add(new StringContent(value), name);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Temp archive cleanup is best effort.
        }
    }
}
