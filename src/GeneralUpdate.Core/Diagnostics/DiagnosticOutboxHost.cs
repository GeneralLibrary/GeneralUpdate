using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>Explicit configuration for a standalone diagnostic scheduler, disabled by default.</summary>
public sealed class DiagnosticOutboxHostOptions
{
    public bool Enabled { get; set; }
    public string FailDirectory { get; set; } = string.Empty;
    public Uri? Endpoint { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan UploadTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public enum DiagnosticOutboxHostStatus
{
    Disabled, Started, ScanStarted, ConfirmedAndDeleted, TombstoneDeleted,
    Deferred, Error, ScanCompleted, Stopped
}

/// <summary>Scheduler notifications never report update success or failure.</summary>
public sealed class DiagnosticOutboxHostEvent
{
    internal DiagnosticOutboxHostEvent(DiagnosticOutboxHostStatus status, string? path = null, Exception? error = null)
    {
        Status = status;
        Path = path;
        Error = error;
    }

    public DiagnosticOutboxHostStatus Status { get; }
    public string? Path { get; }
    public Exception? Error { get; }
}

/// <summary>
/// Replays on startup and scans periodically without running on the caller's update thread.
/// The caller owns HTTP policies, cancellation, notifications and the returned completion task.
/// </summary>
public sealed class DiagnosticOutboxHost
{
    private const string TombstonePrefix = ".generalupdate-uploaded-";
    private readonly string _root;
    private readonly TimeSpan _interval;
    private readonly DiagnosticOutboxConsumer? _consumer;
    private readonly Action<DiagnosticOutboxHostEvent> _notify;
    private int _started;

    public DiagnosticOutboxHost(DiagnosticOutboxHostOptions options,
        Action<DiagnosticOutboxHostEvent> notify, HttpClient? httpClient = null)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _root = string.Empty;
        _interval = options.ScanInterval;
        if (!options.Enabled) return;
        if (_interval < TimeSpan.FromMilliseconds(100) || _interval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(options.ScanInterval));
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        var upload = new ArtifactUploadClient(httpClient,
            options.Endpoint ?? throw new ArgumentException("An explicit endpoint is required.", nameof(options)),
            options.TenantId, options.AppKey);
        _consumer = new DiagnosticOutboxConsumer(options.FailDirectory, upload, options.UploadTimeout);
        _root = Path.GetFullPath(options.FailDirectory);
    }

    /// <summary>
    /// Starts once and returns immediately. Await the task at shutdown; observer exceptions
    /// and unexpected scheduler faults propagate through it. Expected case errors are notified.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The diagnostic host has already been started.");
        return Task.Run(() => RunAsync(cancellationToken));
    }

    private async Task RunAsync(CancellationToken token)
    {
        if (_consumer == null)
        {
            Report(DiagnosticOutboxHostStatus.Disabled);
            return;
        }
        Report(DiagnosticOutboxHostStatus.Started);
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Report(DiagnosticOutboxHostStatus.ScanStarted);
                try { await ScanAsync(token).ConfigureAwait(false); }
                catch (Exception error) when (IsRecoverable(error, token))
                {
                    Report(DiagnosticOutboxHostStatus.Error, _root, error);
                }
                Report(DiagnosticOutboxHostStatus.ScanCompleted);
                await Task.Delay(_interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Host cancellation is normal shutdown, not an update failure.
        }
        finally { Report(DiagnosticOutboxHostStatus.Stopped); }
    }

    private async Task ScanAsync(CancellationToken token)
    {
        DiagnosticOutboxConsumer.CheckDirectoryChain(_root);
        foreach (var versionPath in Directory.EnumerateDirectories(_root))
        {
            token.ThrowIfCancellationRequested();
            var version = Path.GetFileName(versionPath);
            if (version.Equals(".locks", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                DiagnosticOutboxConsumer.CheckDirectoryChain(versionPath);
                foreach (var casePath in Directory.EnumerateDirectories(versionPath))
                {
                    token.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(casePath);
                    var tombstone = name.StartsWith(TombstonePrefix, StringComparison.Ordinal);
                    var id = tombstone ? name.Substring(TombstonePrefix.Length) : name;
                    if (!Guid.TryParseExact(id, "D", out _)) continue;
                    try
                    {
                        DiagnosticOutboxConsumer.CheckDirectoryChain(casePath);
                        var deleted = tombstone
                            ? _consumer!.CleanupConfirmedCase(version, id)
                            : await _consumer!.ProcessCaseAsync(version, id, token).ConfigureAwait(false);
                        Report(deleted
                            ? (tombstone ? DiagnosticOutboxHostStatus.TombstoneDeleted : DiagnosticOutboxHostStatus.ConfirmedAndDeleted)
                            : DiagnosticOutboxHostStatus.Deferred, casePath);
                    }
                    catch (Exception error) when (IsRecoverable(error, token))
                    {
                        Report(DiagnosticOutboxHostStatus.Error, casePath, error);
                    }
                }
            }
            catch (Exception error) when (IsRecoverable(error, token))
            {
                Report(DiagnosticOutboxHostStatus.Error, versionPath, error);
            }
        }
    }

    private static bool IsRecoverable(Exception error, CancellationToken token) =>
        error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
        error is ArgumentException || error is JsonException || error is HttpRequestException ||
        error is OperationCanceledException && !token.IsCancellationRequested;

    private void Report(DiagnosticOutboxHostStatus status, string? path = null, Exception? error = null)
    {
        try { _notify(new DiagnosticOutboxHostEvent(status, path, error)); }
        catch (Exception observerError)
        {
            throw new InvalidOperationException("The diagnostic host observer failed.", observerError);
        }
    }
}
