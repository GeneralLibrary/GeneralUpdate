using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.JsonContext;
using Microsoft.Data.Sqlite;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>
/// Opt-in, single-host outbox for an explicitly authorized ordered-report protocol.
/// All writers for the same server record namespace must share the same local database.
/// </summary>
public sealed class OrderedUpdateReporter : IUpdateReporter
{
    private readonly string _directory;
    private readonly string _identity;
    private readonly HttpUpdateReporter _sender;
    private readonly TimeSpan _lockTimeout;
    private readonly bool _terminalRejectionConfirmed;

    /// <summary>
    /// Creates an ordered reporter. The operator must confirm that the endpoint supports
    /// the event ID and sequence protocol and that namespaceIdentity identifies the stable
    /// tenant and server database namespace. A successful POST is not capability detection.
    /// </summary>
    public OrderedUpdateReporter(string directory, HttpClient client, string reportUrl,
        string namespaceIdentity, bool protocolConfirmed, TimeSpan? lockTimeout = null)
        : this(directory, client, reportUrl, namespaceIdentity, protocolConfirmed, false, lockTimeout)
    {
    }

    /// <summary>Creates a reporter with explicit confirmation of the terminal rejection protocol.</summary>
    public OrderedUpdateReporter(string directory, HttpClient client, string reportUrl,
        string namespaceIdentity, bool protocolConfirmed, bool terminalRejectionConfirmed, TimeSpan? lockTimeout = null)
    {
        if (!protocolConfirmed) throw new InvalidOperationException("The ordered-report protocol must be explicitly confirmed.");
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A private local directory is required.", nameof(directory));
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (!Uri.TryCreate(reportUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("An absolute HTTP report URL is required.", nameof(reportUrl));
        if (string.IsNullOrWhiteSpace(namespaceIdentity))
            throw new ArgumentException("A stable tenant and database namespace identity is required.", nameof(namespaceIdentity));
        _directory = Path.GetFullPath(directory);
        _identity = Identity(reportUrl, namespaceIdentity);
        _sender = new HttpUpdateReporter(client, reportUrl);
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30);
        _terminalRejectionConfirmed = terminalRejectionConfirmed;
        if (_lockTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lockTimeout));
    }

    /// <summary>Atomically allocates an event and sequence, commits the payload, then replays.</summary>
    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (report.EventId != null || report.Sequence != null)
            throw new ArgumentException("The outbox allocates event IDs and sequences.", nameof(report));
        if (report.RecordId <= 0 || (report.Type != 1 && report.Type != 2))
            throw new ArgumentException("A positive record ID and supported record type are required.", nameof(report));

        Directory.CreateDirectory(_directory);
        using (var connection = Open())
        {
            token.ThrowIfCancellationRequested();
            using (var transaction = connection.BeginTransaction())
            {
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = @"INSERT INTO counters (type, record_id, sequence) VALUES ($type, $record, 1)
                        ON CONFLICT(type, record_id) DO UPDATE SET sequence = sequence + 1
                        WHERE sequence < 9223372036854775807;";
                    update.Parameters.AddWithValue("$type", report.Type);
                    update.Parameters.AddWithValue("$record", report.RecordId);
                    if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Report sequence exhausted.");
                }
                long sequence;
                using (var read = connection.CreateCommand())
                {
                    read.Transaction = transaction;
                    read.CommandText = "SELECT sequence FROM counters WHERE type = $type AND record_id = $record;";
                    read.Parameters.AddWithValue("$type", report.Type);
                    read.Parameters.AddWithValue("$record", report.RecordId);
                    sequence = (long)read.ExecuteScalar()!;
                }
                var payload = JsonSerializer.Serialize(report with { EventId = Guid.NewGuid(), Sequence = sequence },
                    UpdateReportJsonContext.Default.UpdateReport);
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO outbox (payload) VALUES ($payload);";
                    insert.Parameters.AddWithValue("$payload", payload);
                    insert.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
        await ReplayAsync(token).ConfigureAwait(false);
    }

    /// <summary>Replays committed events in order, stopping on the first unconfirmed response.</summary>
    public async Task ReplayAsync(CancellationToken token = default)
    {
        using (await AcquireLockAsync(token).ConfigureAwait(false))
        using (var connection = Open())
            await ReplayLockedAsync(connection, token).ConfigureAwait(false);
    }

    private async Task ReplayLockedAsync(SqliteConnection connection, CancellationToken token)
    {
        if (!_terminalRejectionConfirmed && HasQuarantine(connection))
            throw new InvalidOperationException("Quarantined reports require explicit terminal rejection protocol confirmation.");
        while (true)
        {
            token.ThrowIfCancellationRequested();
            long id = 0;
            string? payload = null;
            UpdateReport? report = null;
            var blocked = false;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, payload FROM outbox ORDER BY id;";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var candidate = reader.GetString(1);
                        var parsed = JsonSerializer.Deserialize(candidate, UpdateReportJsonContext.Default.UpdateReport)
                            ?? throw new InvalidDataException("A queued report is null.");
                        if (parsed.RecordId <= 0 || (parsed.Type != 1 && parsed.Type != 2) ||
                            parsed.EventId == null || parsed.EventId == Guid.Empty || parsed.Sequence == null || parsed.Sequence <= 0)
                            throw new InvalidDataException("A queued ordered report is invalid.");
                        if (IsBlocked(connection, parsed.Type, parsed.RecordId))
                        {
                            blocked = true;
                            continue;
                        }
                        id = reader.GetInt64(0);
                        payload = candidate;
                        report = parsed;
                        break;
                    }
                }
            }
            if (payload == null)
            {
                if (blocked || HasQuarantine(connection))
                    throw new InvalidOperationException("Ordered reports are quarantined; operator review is required.");
                return;
            }
            try
            {
                await _sender.SendJsonAsync(payload, token).ConfigureAwait(false);
            }
            catch (HttpUpdateReporter.ReportTerminalConflictException) when (_terminalRejectionConfirmed)
            {
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"INSERT INTO quarantine (id, type, record_id, payload, reason)
                            VALUES ($id, $type, $record, $payload, 'ReportTerminalConflict');
                            DELETE FROM outbox WHERE id = $id;";
                        command.Parameters.AddWithValue("$id", id);
                        command.Parameters.AddWithValue("$type", report!.Type);
                        command.Parameters.AddWithValue("$record", report.RecordId);
                        command.Parameters.AddWithValue("$payload", payload);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                continue;
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM outbox WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidDataException("The queued report changed during delivery.");
            }
        }
    }
            /// <summary>Returns quarantined reports for operator inspection. Payloads may contain sensitive diagnostics.</summary>
            public async Task<IReadOnlyList<QuarantinedUpdateReport>> GetQuarantinedAsync(CancellationToken token = default)
            {
                using (await AcquireLockAsync(token).ConfigureAwait(false))
                using (var connection = Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT id, payload, reason FROM quarantine ORDER BY id;";
                    using (var reader = command.ExecuteReader())
                    {
                        var result = new List<QuarantinedUpdateReport>();
                        while (reader.Read()) result.Add(new QuarantinedUpdateReport(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
                        return result;
                    }
                }
            }

            /// <summary>Moves an inspected event back to its original place in the outbox for an explicit retry.</summary>
            public async Task RetryQuarantinedAsync(long id, CancellationToken token = default)
            {
                using (await AcquireLockAsync(token).ConfigureAwait(false))
                using (var connection = Open())
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "INSERT INTO outbox (id, payload) SELECT id, payload FROM quarantine WHERE id = $id;";
                        command.Parameters.AddWithValue("$id", id);
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Quarantined report not found.");
                        command.CommandText = "DELETE FROM quarantine WHERE id = $id;";
                        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Quarantined report changed.");
                    }
                    transaction.Commit();
                }
            }

            private static bool IsBlocked(SqliteConnection connection, int type, int recordId)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT EXISTS(SELECT 1 FROM quarantine WHERE type = $type AND record_id = $record);";
                    command.Parameters.AddWithValue("$type", type);
                    command.Parameters.AddWithValue("$record", recordId);
                    return (long)command.ExecuteScalar()! != 0;
                }
            }

            private static bool HasQuarantine(SqliteConnection connection)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT EXISTS(SELECT 1 FROM quarantine);";
                    return (long)command.ExecuteScalar()! != 0;
                }
            }
    private SqliteConnection Open()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "ordered-reports.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;";
                command.ExecuteNonQuery();
            }
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"CREATE TABLE IF NOT EXISTS metadata (id INTEGER PRIMARY KEY CHECK(id = 1), identity TEXT NOT NULL);
                        CREATE TABLE IF NOT EXISTS counters (type INTEGER NOT NULL, record_id INTEGER NOT NULL,
                            sequence INTEGER NOT NULL, PRIMARY KEY(type, record_id));
                        CREATE TABLE IF NOT EXISTS outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, payload TEXT NOT NULL);
                        CREATE TABLE IF NOT EXISTS quarantine (id INTEGER PRIMARY KEY, type INTEGER NOT NULL,
                            record_id INTEGER NOT NULL, payload TEXT NOT NULL, reason TEXT NOT NULL);
                        INSERT OR IGNORE INTO metadata (id, identity) VALUES (1, $identity);";
                    command.Parameters.AddWithValue("$identity", _identity);
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT identity FROM metadata WHERE id = 1;";
                    if (!string.Equals(command.ExecuteScalar() as string, _identity, StringComparison.Ordinal))
                        throw new InvalidOperationException("The ordered outbox belongs to a different endpoint or namespace.");
                }
                transaction.Commit();
            }
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(_directory);
        var started = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(Path.Combine(_directory, "ordered-reports.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (started.Elapsed < _lockTimeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
            }
        }
    }

    private static string Identity(string endpoint, string scope)
    {
        using (var hash = SHA256.Create())
        {
            var value = endpoint.Length + ":" + endpoint + scope.Length + ":" + scope;
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
    }

    /// <summary>A rejected report retained for operator review. The JSON is the original wire payload.</summary>
    public sealed record QuarantinedUpdateReport(long Id, string Payload, string Reason);
}
