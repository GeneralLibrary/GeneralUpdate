using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>PID plus exact UTC start time: a recycled PID must never identify a different process as healthy.</summary>
public sealed class ProcessIdentity
{
    /// <summary>OS-assigned process ID; insufficient as an identity on its own.</summary>
    public int Pid { get; set; }
    /// <summary>OS-reported creation time, preserved without rounding across JSON serialization.</summary>
    public DateTimeOffset StartTimeUtc { get; set; }

    /// <summary>Capture identity while the caller still owns the Process handle.</summary>
    internal static ProcessIdentity From(Process process) =>
        new() { Pid = process.Id, StartTimeUtc = process.StartTime.ToUniversalTime() };

    /// <summary>Require both identity fields and current liveness; query failures are not evidence of health.</summary>
    internal bool Matches(Process process) =>
        Pid == process.Id && StartTimeUtc == process.StartTime.ToUniversalTime() && !process.HasExited;
}

/// <summary>Structured local evidence, separate from the legacy server's integer status contract.</summary>
public sealed class UpdateFailure
{
    /// <summary>Originating operation/middleware, retained even after the producer enters failed.</summary>
    public string? Stage { get; set; }
    /// <summary>Distinguishes updateFailure, launchFailure and monitorUnavailable for recovery decisions.</summary>
    public string Category { get; set; } = "updateFailure";
    /// <summary>Fully qualified exception type, without serializing the exception object.</summary>
    public string? ExceptionType { get; set; }
    /// <summary>Exception message after redacting known configured secrets.</summary>
    public string? Message { get; set; }
    /// <summary>Exception.ToString() output, including inner exceptions and stacks, redacted for known secrets.</summary>
    public string? StackTrace { get; set; }
    /// <summary>Original exception HResult; not an HTTP status or an inferred application error code.</summary>
    public int HResult { get; set; }
    /// <summary>Best-known file or target directory; null when the producer cannot identify the affected path.</summary>
    public string? FailedPath { get; set; }
}

/// <summary>Non-secret routing information handed to Bowl; credentials themselves never enter request.json.</summary>
public sealed class BowlReportConfiguration
{
    /// <summary>Optional HTTP(S) endpoint without embedded credentials, query or fragment.</summary>
    public string? Url { get; set; }
    /// <summary>Existing server update record, currently the first main-application package's record ID.</summary>
    public int RecordId { get; set; }
    /// <summary>Trigger type: 1 for polling, 2 for push; unrelated to a package's AppType.</summary>
    public int Type { get; set; } = 1;
    /// <summary>Name of an independently provisioned host credential environment variable, not its value.</summary>
    public string? CredentialEnvironmentVariable { get; set; }
}

/// <summary>Immutable v1 request published before host startup; recovery must not depend on live IPC.</summary>
public sealed class BowlRequest
{
    /// <summary>Protocol version supported by this producer; the host must reject unknown versions.</summary>
    public int ProtocolVersion { get; set; } = 1;
    /// <summary>Canonical GUID matching the containing attempt directory and every peer record.</summary>
    public string AttemptId { get; set; } = string.Empty;
    /// <summary>UTC request creation time for the host's attempt deadline.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>The process whose file application Bowl will supervise.</summary>
    public ProcessIdentity Updater { get; set; } = new();
    /// <summary>Absolute application installation directory, distinct from the durable state root.</summary>
    public string InstallPath { get; set; } = string.Empty;
    /// <summary>Optional directory-mirror snapshot; may be nested under InstallPath and must survive rollback.</summary>
    public string? BackupDirectory { get; set; }
    /// <summary>Version installed before applying this attempt.</summary>
    public string? CurrentVersion { get; set; }
    /// <summary>Version the updater intends to install.</summary>
    public string? TargetVersion { get; set; }
    /// <summary>filesOnly confirms file results; processAlive additionally requires the full survival window.</summary>
    public string LaunchMode { get; set; } = "filesOnly";
    /// <summary>Continuous process-survival window in seconds; not a business-readiness signal.</summary>
    public int HealthTimeoutSeconds { get; set; } = 5;
    /// <summary>Total attempt timeout in seconds, enforced by the external host.</summary>
    public int UpdateTimeoutSeconds { get; set; } = 600;
    /// <summary>Explicit permission for host-owned recovery from the existing backup snapshot.</summary>
    public bool AutoRollback { get; set; }
    /// <summary>Optional legacy endpoint routing; the host owns monitored terminal delivery.</summary>
    public BowlReportConfiguration Report { get; set; } = new();
}

/// <summary>Bowl's readiness acknowledgment; file existence alone is never a successful handshake.</summary>
public sealed class BowlReady
{
    /// <summary>No default version: a missing field must fail the v1 readiness check.</summary>
    public int ProtocolVersion { get; set; }
    /// <summary>Must exactly match the active request's attempt ID.</summary>
    public string AttemptId { get; set; } = string.Empty;
    /// <summary>Must identify the exact live process started by Update, not a launcher child or stale PID.</summary>
    public ProcessIdentity Host { get; set; } = new();
}

/// <summary>Update-only snapshot published atomically; Bowl reads it but owns its own final result separately.</summary>
public sealed class UpdateProducerState
{
    /// <summary>Version the host must validate before interpreting this snapshot.</summary>
    public int ProtocolVersion { get; set; } = 1;
    /// <summary>Correlation ID shared with request.json and ready.json.</summary>
    public string AttemptId { get; set; } = string.Empty;
    /// <summary>preparing/filesApplying/filesApplied/launching/awaitingHealth/completed/failed.</summary>
    public string Stage { get; set; } = "preparing";
    /// <summary>UTC snapshot publication time; not a replacement for checking process liveness.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Exact launched process identity, supplied when entering awaitingHealth.</summary>
    public ProcessIdentity? Application { get; set; }
    /// <summary>Latest relevant failure context; stage determines whether it represents a terminal failure.</summary>
    public UpdateFailure? Error { get; set; }
}

// Source-generated metadata keeps the disk/HTTP contracts usable under trimming and Native AOT.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BowlRequest))]
[JsonSerializable(typeof(BowlReady))]
[JsonSerializable(typeof(UpdateProducerState))]
[JsonSerializable(typeof(UpdateReport))]
[JsonSerializable(typeof(LegacyUpdateReport))]
[JsonSerializable(typeof(BowlOptions))]
internal partial class AttemptJsonContext : JsonSerializerContext;

/// <summary>
/// Local evidence is durable before notification. The external host owns monitored terminal delivery;
/// unmonitored pending reports can be retried explicitly through <see cref="RetryPendingReportsAsync"/>.
/// </summary>
/// <remarks>
/// One role strategy owns this instance. It is not a concurrent journal API: callers must serialize
/// stage changes and reserve pending-report replay for a single scheduler after the producer stops.
/// </remarks>
public sealed class UpdateAttempt : IDisposable
{
    private readonly UpdateContext _config;
    private readonly string _role;
    private Process? _host;
    // Set only after verified readiness; retained to preserve terminal/rollback ownership even if Bowl later exits.
    private ProcessIdentity? _hostIdentity;
    // Publication of request.json transfers delivery ownership before the host can observe the attempt.
    private bool _ownsProducer;
    private UpdateFailure? _lastError;
    private bool _failurePersisted;
    private VersionEntry? _package;
    /// <summary>Attempt-specific directory containing immutable events and, when monitored, protocol files.</summary>
    public string DirectoryPath { get; }
    /// <summary>Current local workflow stage; Client evidence may use stages beyond the monitor protocol.</summary>
    public string Stage { get; private set; } = "preparing";
    /// <summary>Whether readiness was accepted; call EnsureMonitorAlive for current liveness.</summary>
    public bool MonitoringActive => _hostIdentity != null;
    /// <summary>Whether the file-application boundary has been entered, not a count of successfully changed files.</summary>
    public bool FilesModified { get; private set; }

    /// <summary>Resolve and validate storage before any attempt evidence or application files can be written.</summary>
    private UpdateAttempt(UpdateContext config, string role)
    {
        _config = config;
        _role = role;
        config.UpdateAttemptId ??= Guid.NewGuid().ToString("D");
        if (!Guid.TryParseExact(config.UpdateAttemptId, "D", out _))
            throw new ArgumentException("UpdateAttemptId must be a canonical GUID.");
        config.DiagnosticsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeneralUpdate", "state");
        var root = Path.GetFullPath(config.DiagnosticsDirectory);
        ValidateExternalPath(root, config);
        config.DiagnosticsDirectory = root;
        DirectoryPath = Path.Combine(root, "attempts", config.UpdateAttemptId);
        ValidateExternalPath(DirectoryPath, config);
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "events"));
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "pending"));
    }

    /// <summary>Create a role-local journal, reusing IPC identity unless Client is starting a fresh workflow.</summary>
    /// <remarks>Clearing the previous instance first prevents a failed initialization from writing into an older attempt.</remarks>
    internal static UpdateAttempt Begin(UpdateContext config, string role, bool newAttempt = false)
    {
        config.Attempt?.Dispose();
        config.Attempt = null;
        if (newAttempt) config.UpdateAttemptId = Guid.NewGuid().ToString("D");
        var attempt = new UpdateAttempt(config, role);
        config.Attempt = attempt;
        attempt.Record("preparing");
        return attempt;
    }

    /// <summary>Reject overlap in either direction so update/cleanup/rollback cannot remove recovery evidence or the host.</summary>
    /// <remarks>Existing junction/symlink ancestors are rejected; deployment must also keep these directories access-controlled.</remarks>
    internal static void ValidateExternalPath(string path, UpdateContext config)
    {
        if (!Path.IsPathRooted(path))
            throw new ArgumentException("Diagnostic and monitor paths must be absolute.");
        foreach (var value in new[] { config.InstallPath, config.UpdatePath, config.BackupDirectory, config.TempPath })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var target = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(config.InstallPath, value));
            if (ContainsPath(target, path) || ContainsPath(path, target))
                throw new ArgumentException("Diagnostic and monitor paths must not overlap update, staging or backup trees.");
        }
        // Reject symlink/junction ancestors rather than trusting lexical containment checks.
        for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Diagnostic and monitor paths cannot traverse symbolic links or junctions.");
    }

    /// <summary>Compare full directory boundaries, avoiding false matches such as App and Application.</summary>
    private static bool ContainsPath(string parent, string child)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        parent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        child = child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return child.Equals(parent, comparison) ||
               child.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>Publish the request, start the opt-in host and wait for its identity-validated readiness acknowledgment.</summary>
    /// <remarks>
    /// Returns immediately when monitoring is disabled. Otherwise any startup/handshake error is
    /// persisted as monitorUnavailable and rethrown before the caller may apply application files.
    /// </remarks>
    internal async Task StartMonitoringAsync(int recordId, int reportType)
    {
        var options = _config.Monitoring;
        if (options?.Enabled != true) return;
        Stage = "monitorStarting";
        try
        {
            if (!Path.IsPathRooted(options.ExecutablePath) || !File.Exists(options.ExecutablePath))
                throw new FileNotFoundException("The separately deployed Bowl host was not found.", options.ExecutablePath);
            if ((File.GetAttributes(options.ExecutablePath) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("The Bowl executable cannot be a symbolic link.");
            ValidateExternalPath(Path.GetDirectoryName(options.ExecutablePath)!, _config);
            ValidateExternalPath(_config.DiagnosticsDirectory!, _config);
            if (options.ReadyTimeoutSeconds <= 0 || options.UpdateTimeoutSeconds <= options.ReadyTimeoutSeconds ||
                options.HealthTimeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Monitor timeouts must be positive; update timeout must exceed ready timeout.");
            if (!string.IsNullOrWhiteSpace(_config.ReportUrl))
            {
                var uri = new Uri(_config.ReportUrl, UriKind.Absolute);
                if ((uri.Scheme != "http" && uri.Scheme != "https") ||
                    !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    throw new ArgumentException("Bowl reporting URL must be HTTP(S), without credentials, query or fragment.");
            }

            using var current = Process.GetCurrentProcess();
            var request = new BowlRequest
            {
                AttemptId = _config.UpdateAttemptId!,
                Updater = ProcessIdentity.From(current),
                InstallPath = Path.GetFullPath(_config.InstallPath),
                BackupDirectory = _config.BackupDirectory,
                CurrentVersion = _config.ClientVersion,
                TargetVersion = _config.LastVersion,
                LaunchMode = options.VerifyLaunch && _config.LaunchClientAfterUpdate ? "processAlive" : "filesOnly",
                HealthTimeoutSeconds = options.HealthTimeoutSeconds,
                UpdateTimeoutSeconds = options.UpdateTimeoutSeconds,
                AutoRollback = options.AutoRollback && _config.BackupEnabled != false &&
                               Directory.Exists(_config.BackupDirectory),
                Report = new BowlReportConfiguration
                {
                    Url = _config.ReportUrl, RecordId = recordId, Type = reportType,
                    CredentialEnvironmentVariable = options.CredentialEnvironmentVariable
                }
            };
            // Never replace a prior request: reusing an armed attempt must fail rather than adopt stale readiness.
            WriteAtomic(Path.Combine(DirectoryPath, "request.json"), request, AttemptJsonContext.Default.BowlRequest, false);
            _ownsProducer = true;
            Record("preparing");
            var info = new ProcessStartInfo(options.ExecutablePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath)!,
                Arguments = "--attempt " + _config.UpdateAttemptId + " --state-root " + QuoteArgument(_config.DiagnosticsDirectory!)
            };
            _host = Process.Start(info) ?? throw new InvalidOperationException("Bowl host did not start.");
            var expected = ProcessIdentity.From(_host);
            var watch = Stopwatch.StartNew();
            var readyPath = Path.Combine(DirectoryPath, "ready.json");
            while (watch.Elapsed < TimeSpan.FromSeconds(options.ReadyTimeoutSeconds))
            {
                if (_host.HasExited)
                    throw new InvalidOperationException("Bowl host exited before readiness.");
                if (File.Exists(readyPath))
                {
                    // Validate the peer, not merely its file. Start time closes the PID-reuse gap.
                    var ready = JsonSerializer.Deserialize(File.ReadAllText(readyPath), AttemptJsonContext.Default.BowlReady);
                    if (ready == null || ready.ProtocolVersion != 1 || ready.AttemptId != _config.UpdateAttemptId ||
                        ready.Host == null || ready.Host.Pid != expected.Pid ||
                        ready.Host.StartTimeUtc != expected.StartTimeUtc || !ready.Host.Matches(_host))
                        throw new InvalidDataException("Bowl ready identity or protocol does not match this attempt.");
                    _hostIdentity = expected;
                    return;
                }
                await Task.Delay(50).ConfigureAwait(false);
            }
            throw new TimeoutException("Bowl readiness timed out; application files were not modified.");
        }
        catch (Exception ex)
        {
            RecordFailure(ex, "monitorUnavailable");
            if (_host != null && _hostIdentity == null)
            {
                // Only an unarmed process started here is eligible for cleanup; never kill unrelated hosts by name.
                try
                {
                    if (!_host.HasExited) _host.Kill();
                }
                catch (InvalidOperationException stopError) { GeneralTracer.Warn($"Bowl startup cleanup failed: {stopError.Message}"); }
                catch (System.ComponentModel.Win32Exception stopError) { GeneralTracer.Warn($"Bowl startup cleanup failed: {stopError.Message}"); }
            }
            throw;
        }
    }

    /// <summary>Fail closed at update boundaries if a required monitor is unavailable.</summary>
    /// <remarks>Client's in-place updater-package work precedes host ownership; only Update uses this monitor guard.</remarks>
    internal void EnsureMonitorAlive()
    {
        if (_role == "client") return;
        if (_config.Monitoring?.Enabled != true) return;
        bool alive;
        try { alive = _host != null && _hostIdentity != null && _hostIdentity.Matches(_host); }
        catch (InvalidOperationException) { alive = false; }
        catch (System.ComponentModel.Win32Exception) { alive = false; }
        if (!alive)
        {
            var error = new InvalidOperationException("Bowl monitoring is unavailable; further file changes are stopped.");
            RecordFailure(error, "monitorUnavailable");
            throw error;
        }
    }

    /// <summary>Verify monitor availability and persist filesApplying before entering a file-writing operation.</summary>
    internal void BeforeFileChanges()
    {
        EnsureMonitorAlive();
        Record("filesApplying");
        FilesModified = true;
    }

    /// <summary>Append immutable evidence, then atomically publish the latest producer snapshot if this role owns it.</summary>
    /// <remarks>Capture the previous operation as the error origin; failed is a state, not the failed operation itself.</remarks>
    internal void Record(string stage, Exception? error = null, string category = "updateFailure",
        string? failedPath = null, ProcessIdentity? application = null)
    {
        var previousStage = Stage;
        Stage = stage;
        if (stage != "failed") _failurePersisted = false;
        if (stage == "filesApplied" || stage == "completed") _lastError = null;
        if (error != null)
        {
            _lastError = new UpdateFailure
            {
                Category = category, ExceptionType = error.GetType().FullName,
                Stage = error.Data["UpdateStage"] as string ?? previousStage,
                Message = Redact(error.Message), StackTrace = Redact(error.ToString()), HResult = error.HResult,
                FailedPath = Redact((error as FileNotFoundException)?.FileName ??
                    error.Data["UpdateFailedPath"] as string ?? failedPath)
            };
        }
        var status = stage == "failed" && _lastError?.Category != "monitorUnavailable"
            ? UpdateStatus.Failure : UpdateStatus.Updating;
        var report = CreateReport(_package?.RecordId ?? 0, (int)status, _config.ReportType);
        var file = DateTime.UtcNow.Ticks.ToString("D19") + "-" + _role + "-" + Guid.NewGuid().ToString("N") + ".json";
        WriteAtomic(Path.Combine(DirectoryPath, "events", file), report, AttemptJsonContext.Default.UpdateReport, false);
        if (_ownsProducer)
        {
            var state = new UpdateProducerState
            {
                AttemptId = _config.UpdateAttemptId!, Stage = stage, Application = application, Error = _lastError
            };
            WriteAtomic(Path.Combine(DirectoryPath, "producer.json"), state, AttemptJsonContext.Default.UpdateProducerState);
        }
    }

    /// <summary>Best-effort failure persistence that preserves the first error and never prevents recovery handling.</summary>
    /// <remarks>A failed disk write is logged and may be retried; only successful persistence suppresses duplicate calls.</remarks>
    internal void RecordFailure(Exception error, string category = "updateFailure", string? failedPath = null)
    {
        var preserveError = Stage == "failed" && _lastError != null;
        if (preserveError && _failurePersisted) return;
        if (_lastError?.Category == "monitorUnavailable" || (_lastError?.Category == "launchFailure" && category == "updateFailure"))
            category = _lastError.Category;
        try
        {
            Record("failed", preserveError ? null : error, category, failedPath);
            _failurePersisted = true;
        }
        catch (Exception persistenceError)
        {
            GeneralTracer.Error("Failed to persist update failure; recovery evidence may be incomplete.", persistenceError);
        }
    }

    /// <summary>Snapshot known context; generate event identity once here, never during pending-report retries.</summary>
    internal UpdateReport CreateReport(int recordId, int status, int type) =>
        new(recordId, status, type)
        {
            UpdateAttemptId = _config.UpdateAttemptId, Stage = Stage,
            CurrentVersion = _config.ClientVersion, TargetVersion = _config.LastVersion,
            Error = _lastError, Role = _role, TimestampUtc = DateTimeOffset.UtcNow,
            EventId = Guid.NewGuid().ToString("D"), PackageName = _package?.Name, PackageVersion = _package?.Version,
            Outcome = _lastError?.Category == "monitorUnavailable" ? "monitorUnavailable" :
                status == (int)UpdateStatus.Success ? "filesApplied" :
                status == (int)UpdateStatus.Failure ? "updateFailed" : null
        };

    /// <summary>Capture which chain package is active before middleware can fail.</summary>
    internal void SetPackage(VersionEntry package) => _package = package;

    /// <summary>Queue an unmonitored report durably before attempting its bounded, best-effort delivery.</summary>
    /// <remarks>Monitored terminals belong to Bowl. Older pending events block immediate later sends to preserve ordering.</remarks>
    internal async Task ReportAsync(IUpdateReporter reporter, int recordId, int status, int type)
    {
        var report = CreateReport(recordId, status, type);
        // A monitorUnavailable event is not a server-compatible update Failure.
        if (_ownsProducer || _lastError?.Category == "monitorUnavailable") return;
        var pending = Path.Combine(DirectoryPath, "pending",
            DateTime.UtcNow.Ticks.ToString("D19") + "-" + Guid.NewGuid().ToString("N") + ".json");
        WriteAtomic(pending, report, AttemptJsonContext.Default.UpdateReport, false);
        if (Directory.EnumerateFiles(Path.Combine(DirectoryPath, "pending"), "*.json")
            .Any(path => string.CompareOrdinal(path, pending) < 0)) return;
        if (reporter is HttpUpdateReporter http && string.IsNullOrWhiteSpace(http.ReportUrl)) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await reporter.ReportAsync(report, timeout.Token).ConfigureAwait(false);
        // A crash between server acknowledgment and deletion can resend; the legacy API offers no exactly-once guarantee.
        File.Delete(pending);
    }

    /// <summary>
    /// Retry one unmonitored attempt's pending reports in order. Stops on first failure.
    /// Invoke only when its producer has stopped; the caller owns credentials and scheduling.
    /// </summary>
    public static async Task RetryPendingReportsAsync(string attemptDirectory, IUpdateReporter reporter,
        CancellationToken token = default)
    {
        if (File.Exists(Path.Combine(attemptDirectory, "request.json")))
            throw new InvalidOperationException("Bowl owns delivery for monitored attempts.");
        if (reporter is HttpUpdateReporter http && string.IsNullOrWhiteSpace(http.ReportUrl))
            throw new InvalidOperationException("A report endpoint is required for delivery.");
        var attemptId = Path.GetFileName(attemptDirectory.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var path in Directory.EnumerateFiles(Path.Combine(attemptDirectory, "pending"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var report = JsonSerializer.Deserialize(File.ReadAllText(path), AttemptJsonContext.Default.UpdateReport);
            if (report == null || report.ProtocolVersion != 1 || report.UpdateAttemptId != attemptId)
                throw new InvalidDataException("Pending report identity or protocol is invalid.");
            await reporter.ReportAsync(report, token).ConfigureAwait(false);
            File.Delete(path);
        }
    }

    /// <summary>Remove known configured credentials from the new journal, not arbitrary secrets embedded in user exceptions.</summary>
    /// <remarks>Local diagnostics still require restricted access; this is not an authorization to upload stack traces.</remarks>
    private string? Redact(string? value)
    {
        if (value == null) return null;
        foreach (var secret in new[] { _config.Token, _config.AppSecretKey, _config.BasicPassword })
            if (!string.IsNullOrEmpty(secret)) value = value.Replace(secret, "[REDACTED]");
        return value;
    }

    /// <summary>Quote a process argument while preserving embedded quotes and trailing backslashes on netstandard2.0.</summary>
    /// <remarks>Do not use shell escaping here: the host is started with UseShellExecute=false.</remarks>
    internal static string QuoteArgument(string value)
    {
        var builder = new StringBuilder("\"");
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\') { slashes++; continue; }
            builder.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
            builder.Append(ch);
            slashes = 0;
        }
        return builder.Append('\\', slashes * 2).Append('"').ToString();
    }

    /// <summary>Flush a sibling temporary file before publication so readers never observe partially written JSON.</summary>
    /// <remarks>replace=false enforces immutable creation. Readers of replaceable snapshots must permit FileShare.Delete on Windows.</remarks>
    internal static void WriteAtomic<T>(string path, T value, JsonTypeInfo<T> type, bool replace = true)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, type));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (replace && File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Release the local process handle without stopping Bowl; it may still be observing the application.</summary>
    public void Dispose() => _host?.Dispose();
}
