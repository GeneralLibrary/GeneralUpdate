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

public sealed class ProcessIdentity
{
    public int Pid { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }

    internal static ProcessIdentity From(Process process) =>
        new() { Pid = process.Id, StartTimeUtc = process.StartTime.ToUniversalTime() };

    internal bool Matches(Process process) =>
        Pid == process.Id && StartTimeUtc == process.StartTime.ToUniversalTime() && !process.HasExited;
}

public sealed class UpdateFailure
{
    public string? Stage { get; set; }
    public string Category { get; set; } = "updateFailure";
    public string? ExceptionType { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public int HResult { get; set; }
    public string? FailedPath { get; set; }
}

public sealed class BowlReportConfiguration
{
    public string? Url { get; set; }
    public int RecordId { get; set; }
    public int Type { get; set; } = 1;
    public string? CredentialEnvironmentVariable { get; set; }
}

public sealed class BowlRequest
{
    public int ProtocolVersion { get; set; } = 1;
    public string AttemptId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ProcessIdentity Updater { get; set; } = new();
    public string InstallPath { get; set; } = string.Empty;
    public string? BackupDirectory { get; set; }
    public string? CurrentVersion { get; set; }
    public string? TargetVersion { get; set; }
    public string LaunchMode { get; set; } = "filesOnly";
    public int HealthTimeoutSeconds { get; set; } = 5;
    public int UpdateTimeoutSeconds { get; set; } = 600;
    public bool AutoRollback { get; set; }
    public BowlReportConfiguration Report { get; set; } = new();
}

public sealed class BowlReady
{
    public int ProtocolVersion { get; set; }
    public string AttemptId { get; set; } = string.Empty;
    public ProcessIdentity Host { get; set; } = new();
}

public sealed class UpdateProducerState
{
    public int ProtocolVersion { get; set; } = 1;
    public string AttemptId { get; set; } = string.Empty;
    public string Stage { get; set; } = "preparing";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ProcessIdentity? Application { get; set; }
    public UpdateFailure? Error { get; set; }
}

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
public sealed class UpdateAttempt : IDisposable
{
    private readonly UpdateContext _config;
    private readonly string _role;
    private Process? _host;
    private ProcessIdentity? _hostIdentity;
    private bool _ownsProducer;
    private UpdateFailure? _lastError;
    private bool _failurePersisted;
    private VersionEntry? _package;
    public string DirectoryPath { get; }
    public string Stage { get; private set; } = "preparing";
    public bool MonitoringActive => _hostIdentity != null;
    public bool FilesModified { get; private set; }

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

    private static bool ContainsPath(string parent, string child)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        parent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        child = child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return child.Equals(parent, comparison) ||
               child.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }

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

    internal void BeforeFileChanges()
    {
        EnsureMonitorAlive();
        Record("filesApplying");
        FilesModified = true;
    }

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

    internal void SetPackage(VersionEntry package) => _package = package;

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

    private string? Redact(string? value)
    {
        if (value == null) return null;
        foreach (var secret in new[] { _config.Token, _config.AppSecretKey, _config.BasicPassword })
            if (!string.IsNullOrEmpty(secret)) value = value.Replace(secret, "[REDACTED]");
        return value;
    }

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

    public void Dispose() => _host?.Dispose();
}
