using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>
/// Opt-in, disk-backed, ordered at-least-once reporting. The host must replay on startup.
/// Use a separate queue directory for each endpoint and protect it from untrusted writers.
/// </summary>
public sealed class PersistentUpdateReporter : IUpdateReporter
{
    private readonly string _directory;
    private readonly HttpUpdateReporter _sender;
    private readonly TimeSpan _lockTimeout;

    /// <summary>Creates a reporter using the supplied endpoint and queue directory.</summary>
    public PersistentUpdateReporter(string directory, HttpUpdateReporter sender, TimeSpan? lockTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A queue directory is required.", nameof(directory));
        _directory = Path.GetFullPath(directory);
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30);
        if (_lockTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lockTimeout));
    }

    /// <summary>Persists the report before attempting to send the ordered queue.</summary>
    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        using (await AcquireLockAsync(token).ConfigureAwait(false))
        {
            var files = PendingFiles();
            long sequence = 1;
            for (var i = files.Length - 1; i >= 0; i--)
            {
                // A foreign/non-numeric file dropped into the queue directory must not
                // paralyse the pipeline — skip it for sequence derivation (replay skips it too).
                if (long.TryParse(Path.GetFileNameWithoutExtension(files[i]), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    sequence = checked(parsed + 1);
                    break;
                }
            }
            var path = Path.Combine(_directory, sequence.ToString("D20", CultureInfo.InvariantCulture) + ".json");
            var temp = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, UpdateReportJsonContext.Default.UpdateReport));
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            await ReplayLockedAsync(token).ConfigureAwait(false);
        }
    }

    /// <summary>Retries pending reports in order, stopping and throwing on the first failure.</summary>
    public async Task ReplayAsync(CancellationToken token = default)
    {
        using (await AcquireLockAsync(token).ConfigureAwait(false))
            await ReplayLockedAsync(token).ConfigureAwait(false);
    }

    private async Task ReplayLockedAsync(CancellationToken token)
    {
        foreach (var path in PendingFiles())
        {
            token.ThrowIfCancellationRequested();
            UpdateReport? report;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                using (var document = JsonDocument.Parse(json))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !IsInteger(root, "recordId") || !IsInteger(root, "status") || !IsInteger(root, "type"))
                        throw new InvalidDataException("A queued update report is invalid.");
                }
                report = JsonSerializer.Deserialize(json, UpdateReportJsonContext.Default.UpdateReport)
                    ?? throw new InvalidDataException("A queued update report is null.");
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or IOException)
            {
                // A foreign or corrupt file must not paralyse the whole queue: quarantine
                // it (evidence retained under .invalid) and keep replaying the rest.
                var quarantine = Path.Combine(_directory, Path.GetFileName(path) + ".invalid");
                GeneralTracer.Warn($"PersistentUpdateReporter: quarantining unreadable queue entry '{path}'. {ex.Message}");
                if (File.Exists(quarantine)) File.Delete(quarantine);
                File.Move(path, quarantine);
                continue;
            }
            await _sender.SendAsync(report, token).ConfigureAwait(false);
            File.Delete(path);
        }
    }

    private string[] PendingFiles() =>
        Directory.GetFiles(_directory, "*.json").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();

    private static bool IsInteger(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out _);

    private async Task<FileStream> AcquireLockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(_directory);
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(Path.Combine(_directory, "queue.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (started.Elapsed < _lockTimeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
            }
        }
    }
}
