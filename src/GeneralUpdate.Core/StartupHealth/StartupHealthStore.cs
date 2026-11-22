using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GeneralUpdate.Core.StartupHealth;

/// <summary>
/// Stores explicit startup confirmations and failures for cooperating processes.
/// This store does not launch, monitor, report to a server, or roll back an application.
/// </summary>
/// <remarks>
/// Use a local directory outside the installation and backup trees, writable only by trusted
/// participants. All participants must use the same directory and attempt ID. Retain the
/// directory until no participant can access it; do not delete lock files while in use.
/// Timeouts use the local UTC clock and are recorded when a participant next accesses the attempt.
/// </remarks>
public sealed class StartupHealthStore
{
    private const int FormatVersion = 1;
    private readonly string _root;

    /// <summary>Creates a store rooted at a host-controlled, persistent local directory.</summary>
    public StartupHealthStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A persistent directory is required.", nameof(directory));
        _root = Path.GetFullPath(directory);
    }

    /// <summary>Creates an attempt with a future UTC deadline and returns its unique ID.</summary>
    public Guid Begin(DateTimeOffset deadline)
    {
        var id = Guid.NewGuid();
        Begin(id, deadline);
        return id;
    }

    /// <summary>
    /// Creates an attempt under a caller-selected ID. The caller must durably retain the ID
    /// before creation; an ID must never be reused, even if creation is interrupted.
    /// </summary>
    public void Begin(Guid id, DateTimeOffset deadline)
    {
        if (id == Guid.Empty) throw new ArgumentException("An attempt ID is required.", nameof(id));
        if (deadline <= DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(deadline), "The deadline must be in the future.");

        var directory = AttemptDirectory(id);
        Directory.CreateDirectory(directory);
        using (var claim = new FileStream(Path.Combine(directory, "creation"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            claim.Flush(true);
        WriteAtomically(Path.Combine(directory, "attempt"), writer =>
        {
            writer.Write(FormatVersion);
            writer.Write(deadline.UtcDateTime.Ticks);
        });
    }

    /// <summary>
    /// Reads the outcome, recording a timeout if the deadline has elapsed while still pending.
    /// Missing or corrupt attempts and storage failures are reported as exceptions.
    /// </summary>
    public StartupHealthStatus GetStatus(Guid attemptId) => Transition(attemptId, null, out _);

    /// <summary>
    /// Records an explicit business-health confirmation before the deadline. Returns false if
    /// the attempt already has a terminal outcome, including a timeout.
    /// </summary>
    public bool ConfirmHealthy(Guid attemptId)
    {
        Transition(attemptId, StartupHealthStatus.Healthy, out var changed);
        return changed;
    }

    /// <summary>
    /// Records a failed health check or an observed process exit. Returns false if the attempt
    /// already has a terminal outcome. Observing an exit is the host's responsibility.
    /// </summary>
    public bool ReportFailure(Guid attemptId, StartupHealthStatus failure)
    {
        if (failure != StartupHealthStatus.Failed && failure != StartupHealthStatus.ProcessExited)
            throw new ArgumentOutOfRangeException(nameof(failure));
        Transition(attemptId, failure, out var changed);
        return changed;
    }

    private StartupHealthStatus Transition(Guid id, StartupHealthStatus? requested, out bool changed)
    {
        if (id == Guid.Empty) throw new ArgumentException("An attempt ID is required.", nameof(id));
        var directory = AttemptDirectory(id);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Startup attempt {id} was not found.");

        using (AcquireLock(Path.Combine(directory, "lock")))
        {
            var deadline = ReadDeadline(Path.Combine(directory, "attempt"));
            var resultPath = Path.Combine(directory, "result");
            changed = false;
            if (File.Exists(resultPath)) return ReadResult(resultPath);

            var result = DateTimeOffset.UtcNow >= deadline ? StartupHealthStatus.TimedOut : requested;
            if (!result.HasValue) return StartupHealthStatus.Pending;

            WriteAtomically(resultPath, writer =>
            {
                writer.Write(FormatVersion);
                writer.Write((int)result.Value);
            });
            changed = result == requested;
            return result.Value;
        }
    }

    private string AttemptDirectory(Guid id) => Path.Combine(_root, id.ToString("N"));

    private static FileStream AcquireLock(string path)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                Thread.Sleep(10);
            }
        }
    }

    private static DateTimeOffset ReadDeadline(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new BinaryReader(stream))
        {
            if (stream.Length != 12 || reader.ReadInt32() != FormatVersion)
                throw new InvalidDataException("Invalid startup attempt metadata.");
            var ticks = reader.ReadInt64();
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Invalid startup attempt deadline.");
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    private static StartupHealthStatus ReadResult(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new BinaryReader(stream))
        {
            if (stream.Length != 8 || reader.ReadInt32() != FormatVersion)
                throw new InvalidDataException("Invalid startup attempt result.");
            var status = (StartupHealthStatus)reader.ReadInt32();
            if (status != StartupHealthStatus.Healthy && status != StartupHealthStatus.Failed &&
                status != StartupHealthStatus.ProcessExited && status != StartupHealthStatus.TimedOut)
                throw new InvalidDataException("Invalid startup attempt outcome.");
            return status;
        }
    }

    private static void WriteAtomically(string path, Action<BinaryWriter> write)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    write(writer);
                    writer.Flush();
                }
                stream.Flush(true);
            }
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
