using System;
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

    /// <summary>
    /// Creates an ordered reporter. The operator must confirm that the endpoint supports
    /// the event ID and sequence protocol and that namespaceIdentity identifies the stable
    /// tenant and server database namespace. A successful POST is not capability detection.
    /// </summary>
    public OrderedUpdateReporter(string directory, HttpClient client, string reportUrl,
        string namespaceIdentity, bool protocolConfirmed, TimeSpan? lockTimeout = null)
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
        while (true)
        {
            token.ThrowIfCancellationRequested();
            long id;
            string payload;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, payload FROM outbox ORDER BY id LIMIT 1;";
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return;
                    id = reader.GetInt64(0);
                    payload = reader.GetString(1);
                }
            }
            await _sender.SendJsonAsync(payload, token).ConfigureAwait(false);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM outbox WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidDataException("The queued report changed during delivery.");
            }
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
}
