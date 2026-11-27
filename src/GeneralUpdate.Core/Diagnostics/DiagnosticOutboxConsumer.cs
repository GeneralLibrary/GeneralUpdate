using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>Explicit, cooperative consumer for a Bowl diagnostic outbox.</summary>
/// <remarks>The host must protect the outbox against untrusted local writers.</remarks>
public sealed class DiagnosticOutboxConsumer
{
    private const int MaxManifestBytes = 64 * 1024;
    private const int MaxFiles = 128;
    private const int MaxZipBytes = 19 * 1024 * 1024;
    private const string ZipName = ".generalupdate-outbox.zip";
    private const string HashName = ".generalupdate-outbox.sha256";
    private const string TempZipName = ".generalupdate-outbox.zip.tmp";
    private const string TempHashName = ".generalupdate-outbox.sha256.tmp";
    private readonly string _root;
    private readonly ArtifactUploadClient _client;
    private readonly TimeSpan _uploadTimeout;
    private readonly bool _allowLegacy;

    /// <summary>
    /// Creates a consumer. Legacy cases may be enabled only after all legacy writers,
    /// uploaders and collectors have been stopped.
    /// </summary>
    public DiagnosticOutboxConsumer(string failDirectory, ArtifactUploadClient client,
        TimeSpan uploadTimeout, bool allowLegacyAfterQuiescence = false)
    {
        if (string.IsNullOrWhiteSpace(failDirectory)) throw new ArgumentException("An outbox directory is required.", nameof(failDirectory));
        if (uploadTimeout <= TimeSpan.Zero || uploadTimeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(uploadTimeout));
        _root = Path.GetFullPath(failDirectory);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _uploadTimeout = uploadTimeout;
        _allowLegacy = allowLegacyAfterQuiescence;
    }

    /// <summary>
    /// Attempts one case. Returns false when it is busy, incomplete, unmarked, or
    /// the server cannot confirm its hash. Invalid data and I/O errors are surfaced.
    /// </summary>
    public async Task<bool> ProcessCaseAsync(string version, string caseGuid, CancellationToken cancellationToken = default)
    {
        RequireSegment(version, nameof(version));
        if (version.Equals(".locks", StringComparison.OrdinalIgnoreCase) || version.Length > 256)
            throw new ArgumentException("Invalid outbox version.", nameof(version));
        if (!Guid.TryParseExact(caseGuid, "D", out var guid))
            throw new ArgumentException("A D-format case GUID is required.", nameof(caseGuid));
        cancellationToken.ThrowIfCancellationRequested();
        CheckDirectoryChain(_root);
        var stripeLock = TryAcquireLock(guid);
        if (stripeLock == null) return false;
        using (stripeLock)
        {
            var versionPath = Path.Combine(_root, version);
            var casePath = Path.Combine(versionPath, caseGuid);
            if (!Directory.Exists(casePath)) return false;
            CheckDirectoryChain(casePath);
            var manifestPath = Path.Combine(casePath, "manifest.json");
            if (!File.Exists(manifestPath)) return false;
            var manifest = ReadBounded(manifestPath, MaxManifestBytes);
            var metadata = ParseManifest(manifest, version, guid, out var files, out var marked);
            if (!marked && !_allowLegacy) return false;
            ValidateCaseEntries(casePath, files);
            var zipPath = Path.Combine(casePath, ZipName);
            var hashPath = Path.Combine(casePath, HashName);
            RejectLinkIfExists(zipPath);
            RejectLinkIfExists(hashPath);
            var hasZip = File.Exists(zipPath);
            var hasHash = File.Exists(hashPath);
            if (hasZip != hasHash) throw new InvalidDataException("Incomplete persisted snapshot; manual recovery is required.");
            if (!hasZip)
                CreateSnapshot(casePath, manifest, files, cancellationToken);
            var expectedHash = Encoding.ASCII.GetString(ReadBounded(hashPath, 64));
            var zip = ReadBounded(zipPath, MaxZipBytes);
            if (expectedHash.Length != 64 || !string.Equals(expectedHash, Hash(zip), StringComparison.Ordinal))
                throw new InvalidDataException("Persisted snapshot failed integrity verification.");
            using (var archive = new ZipArchive(new MemoryStream(zip, false), ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Snapshot has no manifest.");
                using var input = entry.Open();
                using var bytes = new MemoryStream();
                CopyBounded(input, bytes, MaxManifestBytes, cancellationToken);
                if (!bytes.ToArray().SequenceEqual(manifest))
                    throw new InvalidDataException("The case manifest changed after snapshot creation.");
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_uploadTimeout);
            var result = await _client.UploadAsync(metadata, zip, timeout.Token).ConfigureAwait(false);
            if (!result.HashVerified) return false;
            cancellationToken.ThrowIfCancellationRequested();
            CheckDirectoryChain(casePath);
            ValidateCaseEntries(casePath, files);
            var tombstone = Path.Combine(versionPath, ".generalupdate-uploaded-" + guid.ToString("D"));
            Directory.Move(casePath, tombstone);
            Directory.Delete(tombstone, true);
            return true;
        }
    }

    /// <summary>Removes a previously hash-confirmed tombstone under its original stripe lock.</summary>
    public bool CleanupConfirmedCase(string version, string caseGuid)
    {
        RequireSegment(version, nameof(version));
        if (version.Equals(".locks", StringComparison.OrdinalIgnoreCase) || version.Length > 256)
            throw new ArgumentException("Invalid outbox version.", nameof(version));
        if (!Guid.TryParseExact(caseGuid, "D", out var guid))
            throw new ArgumentException("A D-format case GUID is required.", nameof(caseGuid));
        CheckDirectoryChain(_root);
        using var stripeLock = TryAcquireLock(guid);
        if (stripeLock == null) return false;
        var path = Path.Combine(_root, version, ".generalupdate-uploaded-" + guid.ToString("D"));
        if (!Directory.Exists(path)) return false;
        CheckDirectoryChain(path);
        Directory.Delete(path, true);
        return true;
    }

    private FileStream? TryAcquireLock(Guid guid)
    {
        var locks = Path.Combine(_root, ".locks");
        Directory.CreateDirectory(locks);
        CheckDirectoryChain(locks);
        var lockPath = Path.Combine(locks, guid.ToString("D").Substring(0, 2) + ".lock");
        RejectLinkIfExists(lockPath);
        try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) when (IsLockContention(ex)) { return null; }
    }

    private static ArtifactUploadMetadata ParseManifest(byte[] bytes, string version, Guid guid,
        out List<string> files, out bool marked)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!properties.Add(property.Name)) throw new InvalidDataException("Duplicate manifest property.");
        }
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var schemaValue) || schemaValue != 2 ||
            !root.TryGetProperty("guid", out var id) || id.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(id.GetString(), "D", out var parsed) || parsed != guid ||
            !root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() != "crash" ||
            !root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.String || v.GetString() != version ||
            !root.TryGetProperty("targetName", out var target) || target.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(target.GetString()) || target.GetString()!.Length > 256 ||
            !root.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String || state.GetString() != "pending" ||
            !root.TryGetProperty("files", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid Bowl manifest.");
        marked = false;
        if (root.TryGetProperty("outboxProtocol", out var protocol))
        {
            if (protocol.ValueKind != JsonValueKind.Number || !protocol.TryGetInt32(out var protocolValue) || protocolValue != 1)
                throw new InvalidDataException("Unsupported outbox protocol.");
            marked = true;
        }
        files = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid evidence filename.");
            var name = item.GetString()!;
            RequireSegment(name, "filename");
            if (name.Length > 255 || name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("manifest.json.tmp", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(".generalupdate-outbox.", StringComparison.OrdinalIgnoreCase) ||
                !names.Add(name) || files.Count >= MaxFiles)
                throw new InvalidDataException("Invalid or duplicate evidence filename.");
            files.Add(name);
        }
        return new ArtifactUploadMetadata { Guid = guid.ToString("D"), Kind = "crash", Schema = 2, Version = version, TargetName = target.GetString()! };
    }

    private static void CreateSnapshot(string casePath, byte[] manifest, List<string> files, CancellationToken token)
    {
        var tempZip = Path.Combine(casePath, TempZipName);
        var tempHash = Path.Combine(casePath, TempHashName);
        RejectLinkIfExists(tempZip);
        RejectLinkIfExists(tempHash);
        if (File.Exists(tempZip)) File.Delete(tempZip);
        if (File.Exists(tempHash)) File.Delete(tempHash);
        long total = manifest.Length;
        using (var output = new FileStream(tempZip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
                using (var stream = manifestEntry.Open()) stream.Write(manifest, 0, manifest.Length);
                foreach (var name in files.OrderBy(x => x, StringComparer.Ordinal))
                {
                    token.ThrowIfCancellationRequested();
                    var path = Path.Combine(casePath, name);
                    RejectLinkIfExists(path);
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > MaxZipBytes - total) throw new InvalidDataException("Evidence exceeds the size limit.");
                    var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    total += CopyBounded(input, stream, MaxZipBytes - total, token);
                }
            }
            if (output.Length > MaxZipBytes) throw new InvalidDataException("Snapshot exceeds the size limit.");
            output.Flush(true);
        }
        var hash = Encoding.ASCII.GetBytes(Hash(ReadBounded(tempZip, MaxZipBytes)));
        using (var output = new FileStream(tempHash, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            output.Write(hash, 0, hash.Length);
            output.Flush(true);
        }
        File.Move(tempHash, Path.Combine(casePath, HashName));
        File.Move(tempZip, Path.Combine(casePath, ZipName));
    }

    private static byte[] ReadBounded(string path, int limit)
    {
        RejectLinkIfExists(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new MemoryStream();
        CopyBounded(input, output, limit, CancellationToken.None);
        return output.ToArray();
    }

    private static long CopyBounded(Stream input, Stream output, long limit, CancellationToken token)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            token.ThrowIfCancellationRequested();
            total += read;
            if (total > limit) throw new InvalidDataException("File exceeds the size limit.");
            output.Write(buffer, 0, read);
        }
        return total;
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static bool IsLockContention(IOException error)
    {
        var code = error.HResult & 0xffff;
        return code == 32 || code == 33 || code == 11;
    }

    private static void RequireSegment(string? value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ||
            value!.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || Path.IsPathRooted(value) ||
            value.EndsWith(".", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal))
            throw new ArgumentException("A single safe path segment is required.", parameter);
    }

    private static void ValidateCaseEntries(string casePath, List<string> files)
    {
        var allowed = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase)
        {
            "manifest.json", ZipName, HashName, TempZipName, TempHashName
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFileSystemEntries(casePath))
        {
            var name = Path.GetFileName(path);
            if (!allowed.Contains(name) || !seen.Add(name) ||
                (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Unexpected or unsafe entry in diagnostic case.");
        }
        foreach (var name in files)
            if (!seen.Contains(name)) throw new InvalidDataException("A manifest evidence file is missing.");
    }

    private static void RejectLinkIfExists(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Outbox links and reparse points are not allowed.");
    }

    private static void CheckDirectoryChain(string path)
    {
        var current = new DirectoryInfo(path);
        while (current != null)
        {
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Outbox directory is missing or is a link.");
            current = current.Parent;
        }
    }
}
