using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GeneralUpdate.Core.Compress
{
    /// <summary>
    /// Preflights and stages a complete ZIP without writing to its eventual destination.
    /// The caller must supply a trusted, access-controlled staging parent and keep it
    /// protected from concurrent modification until the returned result is disposed.
    /// ZIP64 and multi-volume archives are not supported. On netstandard2.0, the
    /// runtime must expose ZipArchiveEntry.ExternalAttributes or staging fails closed.
    /// </summary>
    internal static class FullZipStager
    {
        internal static StagedZip Stage(string zipPath, string stagingParent, string expectedSha256,
            int maxEntries, long maxFileBytes, long maxTotalBytes)
        {
            if (string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("ZIP path is required.", nameof(zipPath));
            if (string.IsNullOrWhiteSpace(stagingParent)) throw new ArgumentException("Staging parent is required.", nameof(stagingParent));
            if (maxEntries <= 0) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            if (maxFileBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
            if (maxTotalBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxTotalBytes));
            if (!IsSha256(expectedSha256)) throw new ArgumentException("A SHA-256 hex digest is required.", nameof(expectedSha256));

            using (var zip = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var actual = ToHex(sha.ComputeHash(zip));
                if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ZIP SHA-256 mismatch.");
                zip.Position = 0;
                using (var archive = new ZipArchive(zip, ZipArchiveMode.Read, true))
                {
                    if (archive.Entries.Count > maxEntries) throw new InvalidDataException("ZIP entry limit exceeded.");
                    var records = ReadRecords(zip, archive);
                    var paths = new Dictionary<string, PathKind>(StringComparer.OrdinalIgnoreCase);
                    long declaredTotal = 0;
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        var entry = archive.Entries[i];
                        bool directory = ValidateEntry(entry, paths);
                        if (entry.Length > maxFileBytes || entry.Length > maxTotalBytes - declaredTotal)
                            throw new InvalidDataException("ZIP expanded size limit exceeded.");
                        if (directory && (entry.Length != 0 || records[i].Crc != 0))
                            throw new InvalidDataException("Directory has content.");
                        declaredTotal += entry.Length;
                    }

                    string stageDirectory = CreatePrivateDirectory(stagingParent);
                    var createdFiles = new List<string>();
                    try
                    {
                        var staged = new List<StagedZipEntry>();
                        long total = 0;
                        for (int i = 0; i < archive.Entries.Count; i++)
                        {
                            var entry = archive.Entries[i];
                            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                            string stagedPath = Path.Combine(stageDirectory, Guid.NewGuid().ToString("N"));
                            using (var output = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                            {
                                createdFiles.Add(stagedPath);
                                long size = 0;
                                uint crc = 0xffffffff;
                                zip.Position = records[i].DataOffset;
                                using (var bounded = new BoundedReadStream(zip, records[i].CompressedSize))
                                using (var input = records[i].Method == 8
                                    ? (Stream)new DeflateStream(bounded, CompressionMode.Decompress, true)
                                    : bounded)
                                {
                                    var buffer = new byte[81920];
                                    int read;
                                    while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        if (read > maxFileBytes - size || read > maxTotalBytes - total || read > entry.Length - size)
                                            throw new InvalidDataException("ZIP expanded size limit exceeded.");
                                        output.Write(buffer, 0, read);
                                        for (int j = 0; j < read; j++)
                                            crc = CrcTable[(crc ^ buffer[j]) & 0xff] ^ (crc >> 8);
                                        size += read;
                                        total += read;
                                    }
                                }
                                if (size != entry.Length || (crc ^ 0xffffffff) != records[i].Crc)
                                    throw new InvalidDataException("ZIP entry length or checksum mismatch.");
                                output.Flush();
                                output.Position = 0;
                                staged.Add(new StagedZipEntry(entry.FullName, stagedPath, ToHex(sha.ComputeHash(output))));
                            }
                        }
                        return new StagedZip(stageDirectory, staged, createdFiles);
                    }
                    catch (Exception error)
                    {
                        try { Cleanup(stageDirectory, createdFiles); }
                        catch (Exception cleanupError) { throw new AggregateException(error, cleanupError); }
                        throw;
                    }
                }
            }
        }

        private enum PathKind { ImplicitDirectory, Directory, File }

        private static readonly uint[] CrcTable = CreateCrcTable();

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }

        // ZipArchive may stop at a forged uncompressed length without verifying CRC.
        // Use the central directory to bound the compressed input instead.
        // ZIP64 and multi-volume archives are deliberately rejected.
        private static List<ZipRecord> ReadRecords(Stream zip, ZipArchive archive)
        {
            int tailLength = (int)Math.Min(zip.Length, 65557);
            var tail = new byte[tailLength];
            zip.Position = zip.Length - tailLength;
            using (var reader = new BinaryReader(zip, System.Text.Encoding.UTF8, true))
            {
                int read = 0;
                while (read < tail.Length)
                {
                    int count = zip.Read(tail, read, tail.Length - read);
                    if (count == 0) throw new InvalidDataException("Truncated ZIP.");
                    read += count;
                }
                int end = -1;
                for (int i = tail.Length - 22; i >= 0; i--)
                    if (BitConverter.ToUInt32(tail, i) == 0x06054b50 &&
                        i + 22 + BitConverter.ToUInt16(tail, i + 20) == tail.Length)
                    { end = i; break; }
                if (end < 0) throw new InvalidDataException("ZIP end record not found.");
                ushort disk = BitConverter.ToUInt16(tail, end + 4);
                ushort centralDisk = BitConverter.ToUInt16(tail, end + 6);
                ushort diskEntries = BitConverter.ToUInt16(tail, end + 8);
                ushort entries = BitConverter.ToUInt16(tail, end + 10);
                uint centralSize = BitConverter.ToUInt32(tail, end + 12);
                uint centralOffset = BitConverter.ToUInt32(tail, end + 16);
                long endOffset = zip.Length - tailLength + end;
                if (disk != 0 || centralDisk != 0 || diskEntries != entries || entries == 0xffff ||
                    centralSize == uint.MaxValue || centralOffset == uint.MaxValue ||
                    entries != archive.Entries.Count || (long)centralOffset + centralSize != endOffset)
                    throw new InvalidDataException("Unsupported or invalid ZIP directory.");
                zip.Position = centralOffset;
                var records = new List<ZipRecord>(entries);
                for (int i = 0; i < entries; i++)
                {
                    if (zip.Position > endOffset - 46 || reader.ReadUInt32() != 0x02014b50)
                        throw new InvalidDataException("Invalid ZIP directory entry.");
                    zip.Position += 4;
                    ushort flags = reader.ReadUInt16();
                    ushort method = reader.ReadUInt16();
                    zip.Position += 4;
                    uint crc = reader.ReadUInt32();
                    uint compressed = reader.ReadUInt32();
                    uint expanded = reader.ReadUInt32();
                    ushort nameLength = reader.ReadUInt16();
                    ushort extraLength = reader.ReadUInt16();
                    ushort commentLength = reader.ReadUInt16();
                    ushort entryDisk = reader.ReadUInt16();
                    zip.Position += 6;
                    uint localOffset = reader.ReadUInt32();
                    long next = zip.Position + nameLength + extraLength + commentLength;
                    if (next > endOffset || entryDisk != 0 || localOffset == uint.MaxValue ||
                        compressed == uint.MaxValue || expanded == uint.MaxValue ||
                        (flags & 0x2061) != 0 || (method != 0 && method != 8) ||
                        compressed != archive.Entries[i].CompressedLength || expanded != archive.Entries[i].Length)
                        throw new InvalidDataException("Unsupported or invalid ZIP entry.");
                    byte[] name = reader.ReadBytes(nameLength);
                    if (name.Length != nameLength) throw new InvalidDataException("Truncated ZIP entry name.");
                    records.Add(new ZipRecord(localOffset, compressed, expanded, crc, flags, method, name));
                    zip.Position = next;
                }
                if (zip.Position != endOffset) throw new InvalidDataException("Invalid ZIP directory size.");
                foreach (var record in records)
                {
                    if ((long)record.LocalOffset + 30 > centralOffset)
                        throw new InvalidDataException("Invalid ZIP local header.");
                    zip.Position = record.LocalOffset;
                    if (reader.ReadUInt32() != 0x04034b50) throw new InvalidDataException("Invalid ZIP local header.");
                    zip.Position += 2;
                    ushort flags = reader.ReadUInt16();
                    ushort method = reader.ReadUInt16();
                    zip.Position += 4;
                    uint crc = reader.ReadUInt32();
                    uint compressed = reader.ReadUInt32();
                    uint expanded = reader.ReadUInt32();
                    ushort nameLength = reader.ReadUInt16();
                    ushort extraLength = reader.ReadUInt16();
                    long dataOffset = zip.Position + nameLength + extraLength;
                    if (flags != record.Flags || method != record.Method ||
                        ((flags & 8) == 0 && (crc != record.Crc || compressed != record.CompressedSize || expanded != record.ExpandedSize)) ||
                        nameLength != record.Name.Length || dataOffset + record.CompressedSize > centralOffset)
                        throw new InvalidDataException("Inconsistent ZIP local header.");
                    byte[] name = reader.ReadBytes(nameLength);
                    if (name.Length != record.Name.Length) throw new InvalidDataException("Truncated ZIP entry name.");
                    for (int i = 0; i < name.Length; i++)
                        if (name[i] != record.Name[i]) throw new InvalidDataException("Inconsistent ZIP entry name.");
                    record.DataOffset = dataOffset;
                }
                return records;
            }
        }

        private sealed class ZipRecord
        {
            internal ZipRecord(uint localOffset, uint compressedSize, uint expandedSize, uint crc,
                ushort flags, ushort method, byte[] name)
            {
                LocalOffset = localOffset;
                CompressedSize = compressedSize;
                ExpandedSize = expandedSize;
                Crc = crc;
                Flags = flags;
                Method = method;
                Name = name;
            }
            internal uint LocalOffset { get; }
            internal uint CompressedSize { get; }
            internal uint ExpandedSize { get; }
            internal uint Crc { get; }
            internal ushort Flags { get; }
            internal ushort Method { get; }
            internal byte[] Name { get; }
            internal long DataOffset { get; set; }
        }

        private sealed class BoundedReadStream : Stream
        {
            private readonly Stream _source;
            private long _remaining;
            internal BoundedReadStream(Stream source, long length) { _source = source; _remaining = length; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining == 0) return 0;
                int read = _source.Read(buffer, offset, (int)Math.Min(count, _remaining));
                if (read == 0 && count != 0) throw new InvalidDataException("Truncated ZIP entry.");
                _remaining -= read;
                return read;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static bool ValidateEntry(ZipArchiveEntry entry, Dictionary<string, PathKind> paths)
        {
            string name = entry.FullName;
            bool directory = name.EndsWith("/", StringComparison.Ordinal);
            string path = directory ? name.Substring(0, name.Length - 1) : name;
            if (path.Length == 0 || path.IndexOf('\\') >= 0) throw new InvalidDataException("Invalid ZIP path.");
            string[] parts = path.Split('/');
            string prefix = "";
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                ValidatePart(part);
                prefix = i == 0 ? part : prefix + "/" + part;
                bool last = i == parts.Length - 1;
                if (paths.TryGetValue(prefix, out var existing))
                {
                    if (!last)
                    {
                        if (existing == PathKind.File) throw new InvalidDataException("ZIP file/directory conflict.");
                    }
                    else if (directory && existing == PathKind.ImplicitDirectory)
                        paths[prefix] = PathKind.Directory;
                    else
                        throw new InvalidDataException("Duplicate or conflicting ZIP path.");
                }
                else
                    paths.Add(prefix, last ? (directory ? PathKind.Directory : PathKind.File) : PathKind.ImplicitDirectory);
            }

            int attributes = GetExternalAttributes(entry);
            int unixType = (attributes >> 16) & 0xF000;
            int upperAttributes = (int)((uint)attributes >> 16);
            if ((attributes & 0x400) != 0 || (attributes & 0x40) != 0 ||
                (upperAttributes & 0x400) != 0 || (unixType == 0 && (upperAttributes & 0x40) != 0) ||
                (unixType != 0 && unixType != 0x8000 && unixType != 0x4000) ||
                (unixType == 0x4000 && !directory) || (unixType == 0x8000 && directory) ||
                ((attributes & 0x10) != 0 && !directory))
                throw new InvalidDataException("Unsupported ZIP entry type.");
            return directory;
        }

        private static int GetExternalAttributes(ZipArchiveEntry entry)
        {
#if NETSTANDARD2_0
            // ExternalAttributes is not in the netstandard2.0 reference contract.
            // Never assume an entry is safe on runtimes that cannot expose it.
            var property = typeof(ZipArchiveEntry).GetProperty("ExternalAttributes");
            if (property == null || property.PropertyType != typeof(int))
                throw new PlatformNotSupportedException("ZIP external attributes are unavailable.");
            return (int)property.GetValue(entry)!;
#else
            return entry.ExternalAttributes;
#endif
        }

        private static void ValidatePart(string part)
        {
            if (part.Length == 0 || part == "." || part == ".." ||
                part[part.Length - 1] == '.' || part[part.Length - 1] == ' ' ||
                string.Equals(part, ".backups", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid ZIP path component.");
            foreach (char c in part)
                if (char.IsControl(c) || c == '<' || c == '>' || c == ':' || c == '"' ||
                    c == '|' || c == '?' || c == '*')
                    throw new InvalidDataException("Invalid ZIP path character.");

            string stem = part.Split('.')[0].TrimEnd(' ');
            if (string.Equals(stem, "CON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "PRN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "AUX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "NUL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "CONIN$", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "CONOUT$", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 &&
                 (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 ("123456789\u00b9\u00b2\u00b3".IndexOf(stem[3]) >= 0)))
                throw new InvalidDataException("Reserved Windows name.");
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static string CreatePrivateDirectory(string parent)
        {
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Staging parent does not exist.");
            for (int i = 0; i < 10; i++)
            {
                string path = Path.Combine(parent, "zip-stage-" + Guid.NewGuid().ToString("N"));
                bool created;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    created = CreateDirectoryWindows(path, IntPtr.Zero);
                else
                    created = MkdirUnix(path, 448) == 0; // 0700
                if (created) return path;
                int error = Marshal.GetLastWin32Error();
                if (error != (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 183 : 17))
                    throw new IOException("Unable to create staging directory.", new Win32Exception(error));
            }
            throw new IOException("Unable to allocate a unique staging directory.");
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

        [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
        private static extern int MkdirUnix(string path, int mode);

        internal static void Cleanup(string directory, List<string> files)
        {
            var errors = new List<Exception>();
            foreach (string file in files)
                try { File.Delete(file); } catch (Exception error) { errors.Add(error); }
            try { Directory.Delete(directory, false); }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Unable to clean up ZIP staging.", errors);
        }
    }

    internal sealed class StagedZipEntry
    {
        internal StagedZipEntry(string relativePath, string stagedPath, string sha256)
        {
            RelativePath = relativePath;
            StagedPath = stagedPath;
            Sha256 = sha256;
        }

        internal string RelativePath { get; }
        internal string StagedPath { get; }
        internal string Sha256 { get; }
    }

    internal sealed class StagedZip : IDisposable
    {
        private readonly List<string> _files;
        private bool _retainForRecovery;
        internal StagedZip(string directory, List<StagedZipEntry> entries, List<string> files)
        {
            DirectoryPath = directory;
            Entries = new ReadOnlyCollection<StagedZipEntry>(entries);
            _files = files;
        }

        internal string DirectoryPath { get; }
        internal IReadOnlyList<StagedZipEntry> Entries { get; }
        internal void RetainForRecovery() => _retainForRecovery = true;
        public void Dispose()
        {
            if (!_retainForRecovery) FullZipStager.Cleanup(DirectoryPath, _files);
        }
    }
}
