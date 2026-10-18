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
        var bundle = DiagnosticsPackager.PackBundle(request, _options, _privacyFilter);
        if (bundle == null)
        {
            GeneralTracer.Info("DiagnosticsCollector: nothing to collect for this request.");
            return Task.FromResult<DiagnosticsBundle?>(null);
        }

        var zip = bundle.Value.ZipPath;
        var manifest = new DiagnosticsManifest
        {
            Kind = request.Kind,
            Version = request.TargetVersion,
            Reason = request.Reason,
            Files = bundle.Value.Entries,
        };
        return Task.FromResult<DiagnosticsBundle?>(new DiagnosticsBundle(zip, manifest));
    }
}
