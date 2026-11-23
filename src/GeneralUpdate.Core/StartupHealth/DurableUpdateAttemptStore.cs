using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace GeneralUpdate.Core.StartupHealth;

/// <summary>
/// Durable journal for update attempts and their exact backup snapshots.
/// This store records metadata and fences cooperating recovery claimants; it never restores files.
/// </summary>
public sealed class DurableUpdateAttemptStore
{
    private const int LegacyFormatVersion = 1;
    private const int FormatVersion = 2;
    public const string RequiredAttemptIdEnvironmentVariable = "GENERALUPDATE_REQUIRED_DURABLE_ATTEMPT_ID";
    private readonly string _installPath;
    private readonly string _directory;

    public DurableUpdateAttemptStore(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentException("An installation path is required.", nameof(installPath));
        _installPath = Path.GetFullPath(installPath);
        _directory = GetDirectory(_installPath);
    }

    /// <summary>The stable journal directory used for an installation.</summary>
    public static string GetDirectory(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentException("An installation path is required.", nameof(installPath));
        return Path.Combine(Path.GetFullPath(installPath), GeneralUpdate.Core.FileSystem.StorageManager.BackupRootDirectory,
            GeneralUpdate.Core.FileSystem.StorageManager.AttemptDirectoryName);
    }

    internal static string GetSnapshotManifestPath(string installPath, Guid attemptId)
    {
        ValidateId(attemptId);
        return DurableUpdateSnapshotManifest.ManifestPath(installPath, attemptId);
    }

    /// <summary>
    /// Creates a new attempt. IDs are caller-selected and must never be reused, including
    /// after an interrupted creation.
    /// </summary>
    public void Begin(Guid attemptId)
    {
        ValidateId(attemptId);
        WithLock(() =>
        {
            var path = StatePath(attemptId);
            var creationPath = CreationPath(attemptId);
            if (File.Exists(path) || File.Exists(creationPath))
                throw new InvalidOperationException($"Update attempt {attemptId} already exists.");
            using (var creation = new FileStream(creationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                creation.Flush(true);
            WriteState(path, new AttemptState(attemptId));
        });
    }

    /// <summary>
    /// Pins the intended backup path before copying starts, so concurrent retention cannot
    /// delete it. The path is not a bound snapshot until <see cref="BindSnapshot"/> succeeds.
    /// </summary>
    public void ReserveSnapshot(Guid attemptId, string snapshotPath)
    {
        ValidateId(attemptId);
        var fullPath = ValidateSnapshotPath(snapshotPath);
        WithLock(() =>
        {
            var state = ReadState(attemptId);
            RequireStatus(state, DurableUpdateAttemptStatus.Pending);
            state.SnapshotPath = fullPath;
            state.Status = DurableUpdateAttemptStatus.SnapshotReserved;
            WriteState(StatePath(attemptId), state);
        });
    }

    /// <summary>
    /// Publishes a content manifest and binds it to the exact reserved path after
    /// the real backup operation has completed. No latest-backup fallback is used.
    /// </summary>
    public void BindSnapshot(Guid attemptId, string snapshotPath)
    {
        ValidateId(attemptId);
        var fullPath = ValidateSnapshotPath(snapshotPath);
        WithLock(() =>
        {
            var state = ReadState(attemptId);
            RequireStatus(state, DurableUpdateAttemptStatus.SnapshotReserved);
            if (!PathEquals(state.SnapshotPath!, fullPath))
                throw new InvalidOperationException("The completed backup does not match the reserved snapshot path.");
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException($"The reserved update snapshot was not created: {fullPath}");
            if (state.JournalVersion == LegacyFormatVersion)
                throw new InvalidOperationException("A legacy reserved snapshot cannot be promoted without a content identity.");
            state.SnapshotManifestHash = DurableUpdateSnapshotManifest.CreateAndPublish(_installPath, attemptId, fullPath);
            state.Status = DurableUpdateAttemptStatus.SnapshotBound;
            WriteState(StatePath(attemptId), state);
        });
    }

    /// <summary>
    /// Verifies the bound manifest identity and every file and directory in the exact snapshot.
    /// This detects changed content but does not prevent writes after verification.
    /// </summary>
    public void VerifySnapshotContent(Guid attemptId, string snapshotPath, string expectedManifestHash)
    {
        ValidateId(attemptId);
        var fullPath = ValidateSnapshotPath(snapshotPath);
        WithLock(() =>
        {
            var state = ReadState(attemptId);
            RequireStatus(state, DurableUpdateAttemptStatus.SnapshotBound);
            if (state.SnapshotPath == null || !PathEquals(state.SnapshotPath, fullPath))
                throw new InvalidDataException("The process contract does not match the journal's bound snapshot.");
            if (string.IsNullOrEmpty(state.SnapshotManifestHash))
                throw new InvalidDataException("The bound snapshot has no verifiable content identity.");
            if (!string.Equals(state.SnapshotManifestHash, expectedManifestHash, StringComparison.Ordinal))
                throw new InvalidDataException("The process contract does not match the journal's snapshot content identity.");
            DurableUpdateSnapshotManifest.Verify(_installPath, attemptId, fullPath, state.SnapshotManifestHash);
        });
    }

    /// <summary>Reopens the durable attempt after a process restart.</summary>
    public DurableUpdateAttemptRecord GetAttempt(Guid attemptId)
    {
        ValidateId(attemptId);
        return WithLock(() => ToRecord(ReadState(attemptId)));
    }

    /// <summary>
    /// Claims recovery at the expected fence. Use fence 0 for the first claim. A caller may
    /// supersede a prior claimant only after independently establishing that claimant is no
    /// longer authorized to act. Concurrent claimants using the same fence cannot both win.
    /// </summary>
    public DurableUpdateAttemptClaim ClaimRecovery(Guid attemptId, long expectedFence)
    {
        ValidateId(attemptId);
        if (expectedFence < 0) throw new ArgumentOutOfRangeException(nameof(expectedFence));
        return WithLock(() =>
        {
            var state = ReadState(attemptId);
            if (state.Status != DurableUpdateAttemptStatus.SnapshotBound)
                throw new InvalidOperationException("Recovery requires an active attempt with a bound snapshot.");
            if (state.SnapshotPath == null || !Directory.Exists(state.SnapshotPath))
                throw new DirectoryNotFoundException($"The bound update snapshot is missing: {state.SnapshotPath}");
            if (string.IsNullOrEmpty(state.SnapshotManifestHash))
                throw new InvalidDataException("The bound update snapshot has no verifiable content identity.");
            DurableUpdateSnapshotManifest.Verify(_installPath, attemptId, state.SnapshotPath, state.SnapshotManifestHash);
            if (state.Fence != expectedFence)
                throw new InvalidOperationException($"Stale recovery fence {expectedFence}; current fence is {state.Fence}.");
            state.Fence = checked(state.Fence + 1);
            state.ClaimId = Guid.NewGuid();
            WriteState(StatePath(attemptId), state);
            return new DurableUpdateAttemptClaim(attemptId, state.Fence, state.ClaimId.Value);
        });
    }

    /// <summary>
    /// Closes an attempt only with the current fenced claim. The caller must supply external
    /// evidence of health or completed recovery; this method does not infer either outcome.
    /// </summary>
    public bool Complete(Guid attemptId, DurableUpdateAttemptClaim claim, DurableUpdateAttemptCompletion completion)
    {
        ValidateId(attemptId);
        if (claim == null) throw new ArgumentNullException(nameof(claim));
        if (claim.AttemptId != attemptId) throw new ArgumentException("The claim belongs to another attempt.", nameof(claim));
        if (completion != DurableUpdateAttemptCompletion.HealthyConfirmed &&
            completion != DurableUpdateAttemptCompletion.RecoveryCompleted)
            throw new ArgumentOutOfRangeException(nameof(completion));

        return WithLock(() =>
        {
            var state = ReadState(attemptId);
            if (state.Status == DurableUpdateAttemptStatus.HealthyConfirmed ||
                state.Status == DurableUpdateAttemptStatus.RecoveryCompleted)
                return false;
            if (state.Status != DurableUpdateAttemptStatus.SnapshotBound ||
                state.Fence != claim.Fence || state.ClaimId != claim.ClaimId)
                throw new InvalidOperationException("The recovery claim is stale or no longer owns this attempt.");
            state.Status = completion == DurableUpdateAttemptCompletion.HealthyConfirmed
                ? DurableUpdateAttemptStatus.HealthyConfirmed
                : DurableUpdateAttemptStatus.RecoveryCompleted;
            WriteState(StatePath(attemptId), state);
            return true;
        });
    }

    internal static TResult WithBackupRetentionLock<TResult>(
        string installPath,
        Func<IReadOnlyCollection<string>, TResult> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        var store = new DurableUpdateAttemptStore(installPath);
        using (AcquireLock(store._installPath))
        {
            if (!Directory.Exists(store._directory))
                return action(Array.Empty<string>());

            var pinned = new List<string>();
            foreach (var path in Directory.GetFiles(store._directory, "*.state", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!Guid.TryParseExact(name, "N", out var id))
                    throw new InvalidDataException($"Invalid update attempt journal filename: {Path.GetFileName(path)}");
                var state = ReadStateFile(path, id);
                if (state.Status == DurableUpdateAttemptStatus.SnapshotReserved ||
                    state.Status == DurableUpdateAttemptStatus.SnapshotBound)
                {
                    if (state.SnapshotPath == null)
                        throw new InvalidDataException($"Attempt {state.AttemptId} is missing its pinned snapshot path.");
                    pinned.Add(state.SnapshotPath);
                }
            }
            return action(pinned);
        }
    }

    private string StatePath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".state");
    private string CreationPath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".created");

    private string ValidateSnapshotPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A snapshot path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var backupRoot = Path.GetFullPath(Path.Combine(_installPath, GeneralUpdate.Core.FileSystem.StorageManager.BackupRootDirectory));
        var pathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!PathEquals(Path.GetDirectoryName(fullPath)!, backupRoot) ||
            !Path.GetFileName(fullPath).StartsWith("backup-", pathComparison))
            throw new ArgumentException("The snapshot must be a direct backup-{name} child of the installation backup directory.", nameof(path));
        DurableUpdateSnapshotManifest.EnsureNoReparsePointsInExistingPath(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }

    private void WithLock(Action action) => WithLock(() => { action(); return true; });

    private TResult WithLock<TResult>(Func<TResult> action)
    {
        DurableUpdateSnapshotManifest.EnsureNoReparsePointsInExistingPath(_installPath);
        DurableUpdateSnapshotManifest.EnsureNoReparsePointsInExistingPath(_directory);
        Directory.CreateDirectory(_directory);
        DurableUpdateSnapshotManifest.EnsureNoReparsePointsInPath(_directory);
        using (AcquireLock(_installPath))
            return action();
    }

    private static IDisposable AcquireLock(string installPath)
    {
        var lockIdentity = Path.GetFullPath(installPath).ToUpperInvariant();
        byte[] hash;
        using (var sha = SHA256.Create())
            hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(lockIdentity));
        var mutexName = "GeneralUpdate_Attempt_" + BitConverter.ToString(hash).Replace("-", string.Empty);
        var mutex = new Mutex(false, mutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Timed out waiting for the update-attempt journal lock.");
            }
            catch (AbandonedMutexException)
            {
                // The OS releases a named mutex when its owner process exits.
            }
            return new MutexLock(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private sealed class MutexLock : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _disposed;

        public MutexLock(Mutex mutex) => _mutex = mutex;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _mutex.ReleaseMutex(); }
            finally { _mutex.Dispose(); }
        }
    }

    private AttemptState ReadState(Guid id) => ReadStateFile(StatePath(id), id);

    private static AttemptState ReadStateFile(string path, Guid? expectedId = null)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Durable update attempt was not found.", path);
        try
        {
            DurableUpdateSnapshotManifest.RejectReparsePointIfPresent(path);
            var fileBytes = File.ReadAllBytes(path);
            DurableUpdateSnapshotManifest.RejectReparsePoint(path);
            if (fileBytes.Length < 36) throw new InvalidDataException("Truncated update attempt journal.");
            var payloadLength = fileBytes.Length - 32;
            var payload = new byte[payloadLength];
            var storedHash = new byte[32];
            Buffer.BlockCopy(fileBytes, 0, payload, 0, payloadLength);
            Buffer.BlockCopy(fileBytes, payloadLength, storedHash, 0, storedHash.Length);
            byte[] actualHash;
            using (var sha = SHA256.Create()) actualHash = sha.ComputeHash(payload);
            if (!storedHash.SequenceEqual(actualHash))
                throw new InvalidDataException("Update attempt journal checksum mismatch.");

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            var journalVersion = reader.ReadInt32();
            if (journalVersion != LegacyFormatVersion && journalVersion != FormatVersion)
                throw new InvalidDataException("Unsupported update attempt journal format.");
            var idBytes = reader.ReadBytes(16);
            if (idBytes.Length != 16) throw new InvalidDataException("Truncated update attempt identifier.");
            var id = new Guid(idBytes);
            if (expectedId.HasValue && id != expectedId.Value)
                throw new InvalidDataException("Update attempt identifier does not match its journal filename.");
            var status = (DurableUpdateAttemptStatus)reader.ReadInt32();
            if (!Enum.IsDefined(typeof(DurableUpdateAttemptStatus), status))
                throw new InvalidDataException("Invalid update attempt status.");
            var fence = reader.ReadInt64();
            if (fence < 0) throw new InvalidDataException("Invalid recovery fence.");
            var hasClaim = reader.ReadBoolean();
            Guid? claimId = null;
            if (hasClaim)
            {
                var claimBytes = reader.ReadBytes(16);
                if (claimBytes.Length != 16) throw new InvalidDataException("Truncated recovery claim.");
                claimId = new Guid(claimBytes);
            }
            var hasSnapshot = reader.ReadBoolean();
            var snapshotPath = hasSnapshot ? reader.ReadString() : null;
            string? snapshotManifestHash = null;
            if (journalVersion >= FormatVersion && reader.ReadBoolean())
            {
                snapshotManifestHash = reader.ReadString();
                if (snapshotManifestHash.Length != 64 ||
                    snapshotManifestHash.Any(character => !((character >= '0' && character <= '9') ||
                                                            (character >= 'A' && character <= 'F'))))
                    throw new InvalidDataException("Invalid update snapshot manifest identity.");
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing update attempt data.");
            if ((status == DurableUpdateAttemptStatus.Pending) != (snapshotPath == null))
                throw new InvalidDataException("Update attempt snapshot state is inconsistent.");
            if ((fence == 0) != !claimId.HasValue)
                throw new InvalidDataException("Update attempt recovery claim state is inconsistent.");
            return new AttemptState(id)
            {
                Status = status,
                Fence = fence,
                ClaimId = claimId,
                SnapshotPath = snapshotPath,
                SnapshotManifestHash = snapshotManifestHash,
                JournalVersion = journalVersion
            };
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Truncated update attempt journal.", ex);
        }
    }

    private static void WriteState(string path, AttemptState state)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] payload;
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(state.JournalVersion);
                    writer.Write(state.AttemptId.ToByteArray());
                    writer.Write((int)state.Status);
                    writer.Write(state.Fence);
                    writer.Write(state.ClaimId.HasValue);
                    if (state.ClaimId.HasValue) writer.Write(state.ClaimId.Value.ToByteArray());
                    writer.Write(state.SnapshotPath != null);
                    if (state.SnapshotPath != null) writer.Write(state.SnapshotPath);
                    if (state.JournalVersion >= FormatVersion)
                    {
                        writer.Write(state.SnapshotManifestHash != null);
                        if (state.SnapshotManifestHash != null) writer.Write(state.SnapshotManifestHash);
                    }
                    writer.Flush();
                }
                payload = memory.ToArray();
            }

            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Write(hash, 0, hash.Length);
                stream.Flush(true);
            }
            DurableUpdateSnapshotManifest.RejectReparsePointIfPresent(path);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RequireStatus(AttemptState state, DurableUpdateAttemptStatus status)
    {
        if (state.Status != status)
            throw new InvalidOperationException($"Attempt {state.AttemptId} is {state.Status}, expected {status}.");
    }

    private static DurableUpdateAttemptRecord ToRecord(AttemptState state) =>
        new(state.AttemptId, state.Status, state.SnapshotPath, state.Fence, state.ClaimId, state.SnapshotManifestHash);

    internal static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool PathEquals(string left, string right) => PathsEqual(left, right);

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A non-empty update attempt ID is required.", nameof(id));
    }

    private sealed class AttemptState
    {
        public AttemptState(Guid attemptId) => AttemptId = attemptId;
        public Guid AttemptId { get; }
        public DurableUpdateAttemptStatus Status { get; set; } = DurableUpdateAttemptStatus.Pending;
        public string? SnapshotPath { get; set; }
        public string? SnapshotManifestHash { get; set; }
        public long Fence { get; set; }
        public Guid? ClaimId { get; set; }
        public int JournalVersion { get; set; } = FormatVersion;
    }
}

public enum DurableUpdateAttemptStatus
{
    Pending = 0,
    SnapshotReserved = 1,
    SnapshotBound = 2,
    HealthyConfirmed = 3,
    RecoveryCompleted = 4
}

public enum DurableUpdateAttemptCompletion
{
    HealthyConfirmed = 1,
    RecoveryCompleted = 2
}

public sealed class DurableUpdateAttemptRecord
{
    internal DurableUpdateAttemptRecord(Guid attemptId, DurableUpdateAttemptStatus status, string? snapshotPath, long fence,
        Guid? claimId, string? snapshotManifestHash)
    {
        AttemptId = attemptId;
        Status = status;
        SnapshotPath = snapshotPath;
        Fence = fence;
        ClaimId = claimId;
        SnapshotManifestHash = snapshotManifestHash;
    }

    public Guid AttemptId { get; }
    public DurableUpdateAttemptStatus Status { get; }
    public string? SnapshotPath { get; }
    public long Fence { get; }
    public Guid? ClaimId { get; }
    public string? SnapshotManifestHash { get; }
}

public sealed class DurableUpdateAttemptClaim
{
    internal DurableUpdateAttemptClaim(Guid attemptId, long fence, Guid claimId)
    {
        AttemptId = attemptId;
        Fence = fence;
        ClaimId = claimId;
    }

    public Guid AttemptId { get; }
    public long Fence { get; }
    public Guid ClaimId { get; }
}
