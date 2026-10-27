using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Collects update / self-check diagnostics into a self-contained archive.
/// </summary>
/// <remarks>
///     The default implementation (<see cref="DiagnosticsCollector"/>) assembles the standard
///     artifacts — self-check report, rollback result, trace-log tail, manifest snapshot — hosts
///     may implement the interface to capture business-specific payloads instead.
/// </remarks>
public interface IDiagnosticsCollector
{
    /// <summary>
    ///     Builds the diagnostics bundle. Returns <c>null</c> when there is nothing to send
    ///     (never throws for missing artifacts — diagnostics collection must not disturb the
    ///     main flow).
    /// </summary>
    Task<DiagnosticsBundle?> CollectAsync(DiagnosticsCollectRequest request, CancellationToken ct);
}

/// <summary>
///     Default collector: stages the request payloads plus install-directory artifacts into a
///     temporary directory and hands off to <see cref="DiagnosticsPackager"/> for archiving.
/// </summary>
public sealed class DiagnosticsCollector : IDiagnosticsCollector
{
    private readonly DiagnosticsOptions _options;
    private readonly IBlackMatcher? _privacyFilter;

    /// <param name="options">Runtime options (size cap). Defaults when not supplied.</param>
    /// <param name="privacyFilter">
    ///     Optional blacklist filter for the archive contents. The bootstrap passes the host's
    ///     matcher so user-configured exclusions apply to diagnostics as well.
    /// </param>
    public DiagnosticsCollector(DiagnosticsOptions? options = null, IBlackMatcher? privacyFilter = null)
    {
        _options = options ?? new DiagnosticsOptions();
        _privacyFilter = privacyFilter;
    }

    /// <inheritdoc />
    public Task<DiagnosticsBundle?> CollectAsync(DiagnosticsCollectRequest request, CancellationToken ct)
    {
        // The GUID is generated once here and embedded both in the archive manifest and in the
        // upload metadata — the server deduplicates on it, so a retry re-sends the same id.
        var bundleGuid = Guid.NewGuid().ToString("D");
        var bundle = DiagnosticsPackager.PackBundle(request, _options, bundleGuid, _privacyFilter);
        if (bundle == null)
        {
            GeneralTracer.Info("DiagnosticsCollector: nothing to collect for this request.");
            return Task.FromResult<DiagnosticsBundle?>(null);
        }

        var zip = bundle.Value.ZipPath;
        var manifest = new DiagnosticsManifest
        {
            Guid = bundleGuid,
            Kind = request.Kind,
            Version = request.TargetVersion,
            TargetName = _options.TargetName,
            AppKey = _options.AppKey,
            ProductId = _options.ProductId,
            Platform = _options.Platform,
            RecordId = request.RecordId > 0 ? request.RecordId : null,
            RecordType = request.RecordType > 0 ? request.RecordType : null,
            Reason = request.Reason,
            Files = bundle.Value.Entries,
        };
        return Task.FromResult<DiagnosticsBundle?>(new DiagnosticsBundle(zip, manifest));
    }
}
