using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GeneralUpdate.Core.StartupHealth;

internal static class DurableUpdateSnapshotManifest
{
    private const int FormatVersion = 1;
    private const int MaximumManifestBytes = 64 * 1024 * 1024;
    private const int MaximumEntries = 500_000;

    internal static string ManifestPath(string installPath, Guid attemptId) =>
        Path.Combine(DurableUpdateAttemptStore.GetDirectory(installPath), attemptId.ToString("N") + ".manifest");

    internal static string CreateAndPublish(string installPath, Guid attemptId, string snapshotPath)
    {
        var fullInstallPath = Path.GetFullPath(installPath);
        var fullSnapshotPath = Path.GetFullPath(snapshotPath);
        EnsureNoReparsePointsInPath(fullInstallPath);
        EnsureNoReparsePointsInPath(fullSnapshotPath);

        var entries = CaptureEntries(fullSnapshotPath);
        var bytes = Serialize(attemptId, fullInstallPath, fullSnapshotPath, entries);
        if (bytes.Length > MaximumManifestBytes)
            throw new InvalidDataException("The update snapshot manifest exceeds the supported size.");

        var path = ManifestPath(fullInstallPath, attemptId);
        var directory = Path.GetDirectoryName(path)!;
        EnsureNoReparsePointsInPath(directory);
        RejectReparsePointIfPresent(path);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        return ToHex(Hash(bytes));
    }

    internal static void Verify(
        string installPath,
        Guid attemptId,
        string snapshotPath,
        string expectedManifestHash)
    {
        if (!IsSha256(expectedManifestHash))
            throw new InvalidDataException("The update snapshot manifest identity is missing or malformed.");

        var fullInstallPath = Path.GetFullPath(installPath);
        var fullSnapshotPath = Path.GetFullPath(snapshotPath);
        EnsureNoReparsePointsInPath(fullInstallPath);
        EnsureNoReparsePointsInPath(fullSnapshotPath);

        var path = ManifestPath(fullInstallPath, attemptId);
        EnsureNoReparsePointsInExistingPath(Path.GetDirectoryName(path)!);
        RejectReparsePoint(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The update snapshot manifest is missing.", path);
        if (info.Length <= 0 || info.Length > MaximumManifestBytes)
            throw new InvalidDataException("The update snapshot manifest has an unsupported size.");

        var bytes = File.ReadAllBytes(path);
        RejectReparsePoint(path);
        if (!string.Equals(ToHex(Hash(bytes)), expectedManifestHash, StringComparison.Ordinal))
            throw new InvalidDataException("The update snapshot manifest identity does not match the attempt journal.");

        var manifest = Deserialize(bytes);
        if (manifest.AttemptId != attemptId ||
            !PathEquals(manifest.InstallPath, fullInstallPath) ||
            !PathEquals(manifest.SnapshotPath, fullSnapshotPath))
            throw new InvalidDataException("The update snapshot manifest is bound to another attempt, installation, or snapshot.");

        VerifyEntries(fullSnapshotPath, manifest.Entries);
    }

    internal static void EnsureNoReparsePointsInExistingPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root)) throw new InvalidDataException("A filesystem-rooted path is required.");
        var current = root;
        if (File.Exists(root) || Directory.Exists(root)) RejectReparsePoint(root);
        var remainder = fullPath.Substring(root.Length);
        foreach (var segment in remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                RejectReparsePoint(current);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
        }
    }

    internal static void EnsureNoReparsePointsInPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        EnsureNoReparsePointsInExistingPath(fullPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"The required directory does not exist: {fullPath}");

        var root = Path.GetPathRoot(fullPath)!;
        var current = root;
        var remainder = fullPath.Substring(root.Length);
        foreach (var segment in remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.Directory) == 0)
                throw new InvalidDataException($"A directory was expected in the path: {current}");
        }
    }

    internal static void RejectReparsePoint(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Reparse points are not permitted in durable update snapshots: {path}");
    }

    internal static void RejectReparsePointIfPresent(string path)
    {
        try
        {
            RejectReparsePoint(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
    }

    private static List<Entry> CaptureEntries(string root)
    {
        var entries = new List<Entry>();
        CaptureDirectory(root, string.Empty, entries);
        var ordered = entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToList();
        var portablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ordered)
        {
            if (!portablePaths.Add(entry.Path))
                throw new InvalidDataException($"Snapshot paths collide on a case-insensitive filesystem: {entry.Path}");
        }
        return ordered;
    }

    private static void CaptureDirectory(string directory, string relativeDirectory, List<Entry> entries)
    {
        EnsureNoReparsePointsInPath(directory);
        foreach (var path in Directory.GetFileSystemEntries(directory)
                     .OrderBy(entry => Path.GetFileName(entry), StringComparer.Ordinal))
        {
            RejectReparsePoint(path);
            var name = Path.GetFileName(path);
            ValidateSegment(name);
            var relativePath = relativeDirectory.Length == 0 ? name : relativeDirectory + "/" + name;
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                EnsureEntryLimit(entries);
                entries.Add(new Entry(relativePath, true, 0, Array.Empty<byte>()));
                CaptureDirectory(path, relativePath, entries);
                RejectReparsePoint(path);
            }
            else
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var length = stream.Length;
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(stream);
                RejectReparsePoint(path);
                if (new FileInfo(path).Length != length)
                    throw new IOException($"A snapshot file changed while its manifest was being created: {path}");
                EnsureEntryLimit(entries);
                entries.Add(new Entry(relativePath, false, length, hash));
            }
        }
        EnsureNoReparsePointsInPath(directory);
    }

    private static byte[] Serialize(Guid attemptId, string installPath, string snapshotPath, IReadOnlyList<Entry> entries)
    {
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
        {
            writer.Write(FormatVersion);
            writer.Write(attemptId.ToByteArray());
            writer.Write(installPath);
            writer.Write(snapshotPath);
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                writer.Write(entry.Path);
                writer.Write(entry.IsDirectory);
                if (entry.IsDirectory) continue;
                writer.Write(entry.Length);
                writer.Write(entry.Hash);
            }
        }
        return memory.ToArray();
    }

    private static Manifest Deserialize(byte[] bytes)
    {
        try
        {
            using var memory = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(memory, Encoding.UTF8);
            if (reader.ReadInt32() != FormatVersion)
                throw new InvalidDataException("Unsupported update snapshot manifest format.");
            var idBytes = reader.ReadBytes(16);
            if (idBytes.Length != 16) throw new InvalidDataException("Truncated update snapshot manifest attempt ID.");
            var attemptId = new Guid(idBytes);
            var installPath = reader.ReadString();
            var snapshotPath = reader.ReadString();
            var count = reader.ReadInt32();
            if (count < 0 || count > MaximumEntries)
                throw new InvalidDataException("The update snapshot manifest has an unsupported entry count.");

            var entries = new List<Entry>(count);
            var portablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? previousPath = null;
            for (var index = 0; index < count; index++)
            {
                var relativePath = reader.ReadString();
                ValidateRelativePath(relativePath);
                if (!portablePaths.Add(relativePath))
                    throw new InvalidDataException("Update snapshot manifest paths collide on a case-insensitive filesystem.");
                if (previousPath != null && string.Compare(previousPath, relativePath, StringComparison.Ordinal) >= 0)
                    throw new InvalidDataException("Update snapshot manifest entries are not uniquely sorted.");
                previousPath = relativePath;
                var isDirectory = reader.ReadBoolean();
                if (isDirectory)
                {
                    entries.Add(new Entry(relativePath, true, 0, Array.Empty<byte>()));
                    continue;
                }

                var length = reader.ReadInt64();
                var hash = reader.ReadBytes(32);
                if (length < 0 || hash.Length != 32)
                    throw new InvalidDataException("Invalid update snapshot manifest file entry.");
                entries.Add(new Entry(relativePath, false, length, hash));
            }
            if (memory.Position != memory.Length)
                throw new InvalidDataException("Unexpected trailing update snapshot manifest data.");
            return new Manifest(attemptId, installPath, snapshotPath, entries);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Truncated update snapshot manifest.", ex);
        }
    }

    private static void VerifyEntries(string root, IReadOnlyList<Entry> expectedEntries)
    {
        var actualEntries = CaptureEntries(root);
        if (actualEntries.Count != expectedEntries.Count)
            throw new InvalidDataException("The update snapshot contents do not match the bound manifest.");

        for (var index = 0; index < expectedEntries.Count; index++)
        {
            var expected = expectedEntries[index];
            var actual = actualEntries[index];
            if (!string.Equals(expected.Path, actual.Path, StringComparison.Ordinal) ||
                expected.IsDirectory != actual.IsDirectory ||
                expected.Length != actual.Length ||
                !FixedTimeEquals(expected.Hash, actual.Hash))
                throw new InvalidDataException($"The update snapshot entry does not match its manifest: {expected.Path}");
        }
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidDataException("An update snapshot manifest path is not relative.");
        foreach (var segment in path.Split('/'))
            ValidateSegment(segment);
    }

    private static void ValidateSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." ||
            segment.IndexOfAny(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0' }) >= 0 ||
            segment.Any(character => character < 32) ||
            segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal) ||
            !string.Equals(segment, segment.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
            throw new InvalidDataException("A snapshot path is not in the supported canonical form.");
        var baseName = segment.Split('.')[0];
        if (baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            IsNumberedDeviceName(baseName, "COM") || IsNumberedDeviceName(baseName, "LPT"))
            throw new InvalidDataException("A snapshot path uses a reserved cross-platform device name.");
    }

    private static bool IsNumberedDeviceName(string name, string prefix) =>
        name.Length == prefix.Length + 1 &&
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        name[prefix.Length] >= '1' && name[prefix.Length] <= '9';

    private static void EnsureEntryLimit(IReadOnlyCollection<Entry> entries)
    {
        if (entries.Count >= MaximumEntries)
            throw new InvalidDataException("The update snapshot has too many manifest entries.");
    }

    private static bool IsSha256(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (var character in value)
            if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'F')))
                return false;
        return true;
    }

    private static byte[] Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(bytes);
    }

    private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        var difference = 0;
        for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
        return difference == 0;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed class Entry
    {
        internal Entry(string path, bool isDirectory, long length, byte[] hash)
        {
            Path = path;
            IsDirectory = isDirectory;
            Length = length;
            Hash = hash;
        }

        internal string Path { get; }
        internal bool IsDirectory { get; }
        internal long Length { get; }
        internal byte[] Hash { get; }
    }

    private sealed class Manifest
    {
        internal Manifest(Guid attemptId, string installPath, string snapshotPath, List<Entry> entries)
        {
            AttemptId = attemptId;
            InstallPath = installPath;
            SnapshotPath = snapshotPath;
            Entries = entries;
        }

        internal Guid AttemptId { get; }
        internal string InstallPath { get; }
        internal string SnapshotPath { get; }
        internal List<Entry> Entries { get; }
    }
}
