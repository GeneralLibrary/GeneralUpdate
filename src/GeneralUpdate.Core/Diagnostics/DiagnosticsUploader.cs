using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.JsonContext;
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
///         Wire format: the zip archive as the request body (octet-stream) plus the declared
///         <see cref="DiagnosticsManifest"/> in the <c>X-Diagnostics-Manifest</c> header so the
///         server can audit the contents. Requests carry the global authentication provider and
///         extra headers (same convention as <c>HttpUpdateReporter</c>).
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
    ///     Uploads a bundle (archive + manifest). Deletes the temporary archive afterwards.
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
            using var stream = File.OpenRead(bundle.ZipPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
            {
                Content = new StreamContent(stream)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-Diagnostics-Manifest", SerializeManifest(bundle.Manifest));

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(RequestTimeout);
            using var response = await _client.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                GeneralTracer.Info($"DiagnosticsUploader: bundle uploaded ({bundle.Manifest.Files.Count} file(s)).");
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

    private static string SerializeManifest(DiagnosticsManifest manifest)
    {
        try
        {
            return JsonSerializer.Serialize(manifest, DiagnosticsJsonContext.Default.DiagnosticsManifest);
        }
        catch
        {
            // A manifest is metadata; an upload without it is still worth attempting.
            return "{}";
        }
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
