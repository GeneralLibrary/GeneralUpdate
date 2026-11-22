using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace GeneralUpdate.Core.Compress
{
    // Internal foundation only: the caller must enforce exclusive access to all paths
    // and protect the state, stage and snapshot directories against other writers.
    // This is neither an authorization boundary nor a recovery implementation.
    internal sealed class ControlledFullZipPlan
    {
        internal ControlledFullZipPlan(string targetRoot, string stateDirectory,
            IEnumerable<ControlledFullZipEntry> entries, IEnumerable<string> protectedPaths)
        {
            TargetRoot = targetRoot;
            StateDirectory = stateDirectory;
            Entries = new ReadOnlyCollection<ControlledFullZipEntry>(new List<ControlledFullZipEntry>(entries));
            ProtectedPaths = new ReadOnlyCollection<string>(new List<string>(protectedPaths));
        }

        internal string TargetRoot { get; }
        internal string StateDirectory { get; }
        internal IReadOnlyList<ControlledFullZipEntry> Entries { get; }
        // Relative paths; a protected directory protects its descendants.
        internal IReadOnlyList<string> ProtectedPaths { get; }
    }

    internal sealed class ControlledFullZipEntry
    {
        internal ControlledFullZipEntry(string relativePath, string stagedPath, string payloadSha256,
            string? snapshotPath, string? preStateSha256)
        {
            RelativePath = relativePath;
            StagedPath = stagedPath;
            PayloadSha256 = payloadSha256;
            SnapshotPath = snapshotPath;
            PreStateSha256 = preStateSha256;
        }

        internal string RelativePath { get; }
        internal string StagedPath { get; }
        internal string PayloadSha256 { get; }
        // Both null means the target must not exist; otherwise both are required.
        internal string? SnapshotPath { get; }
        internal string? PreStateSha256 { get; }
    }

    internal static class ControlledFullZipApply
    {
        // Callbacks are for internal fault-injection tests; never use them as fencing.
        internal static string Apply(ControlledFullZipPlan plan, Action? afterIntent = null,
            Action<int>? afterWrite = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (Path.DirectorySeparatorChar != '\\') throw new PlatformNotSupportedException("Windows only.");
            string root = Absolute(plan.TargetRoot);
            string state = Absolute(plan.StateDirectory);
            if (Overlaps(root, state)) throw new InvalidDataException("State and target directories overlap.");
            CheckDirectories(root);
            CheckDirectories(state);
            var protectedPaths = new List<string>();
            foreach (string value in plan.ProtectedPaths)
                protectedPaths.Add(Relative(value));
            // Backup data must never become an apply target even if the caller omits it.
            protectedPaths.Add(".backups");
            var names = new List<string>();
            var records = new List<IntentEntry>();
            var inputs = new List<FileStream>();
            try
            {
                foreach (var entry in plan.Entries)
                {
                    if (entry == null) throw new InvalidDataException("Null entry.");
                    string relative = Relative(entry.RelativePath);
                    foreach (string previous in names)
                        if (Overlaps(previous, relative)) throw new InvalidDataException("Conflicting target paths.");
                    foreach (string protect in protectedPaths)
                        if (Overlaps(protect, relative)) throw new InvalidDataException("Protected target path.");
                    names.Add(relative);
                    string target = Path.Combine(root, relative);
                    CheckDirectories(Path.GetDirectoryName(target)!);
                    string stage = Absolute(entry.StagedPath);
                    if (Overlaps(root, stage) || Overlaps(state, stage))
                        throw new InvalidDataException("Staged file overlaps target or state.");
                    CheckDirectories(Path.GetDirectoryName(stage)!);
                    CheckHash(entry.PayloadSha256);
                    var staged = OpenRegular(stage, false);
                    inputs.Add(staged);
                    Verify(staged, entry.PayloadSha256);
                    bool existing = entry.SnapshotPath != null || entry.PreStateSha256 != null;
                    string? snapshot = null;
                    if (existing)
                    {
                        if (entry.SnapshotPath == null || entry.PreStateSha256 == null)
                            throw new InvalidDataException("Snapshot and pre-state hash are both required.");
                        CheckHash(entry.PreStateSha256);
                        snapshot = Absolute(entry.SnapshotPath);
                        if (Overlaps(root, snapshot) || Overlaps(state, snapshot))
                            throw new InvalidDataException("Snapshot overlaps target or state.");
                        CheckDirectories(Path.GetDirectoryName(snapshot)!);
                        var snap = OpenRegular(snapshot, true);
                        inputs.Add(snap);
                        Verify(snap, entry.PreStateSha256);
                        snap.Flush(true);
                    }
                    VerifyTarget(target, entry.PreStateSha256);
                    string temp = Path.Combine(Path.GetDirectoryName(target)!, ".gu-apply-" + Guid.NewGuid().ToString("N"));
                    records.Add(new IntentEntry
                    {
                        RelativePath = relative, TargetPath = target, StagedPath = stage,
                        PayloadSha256 = entry.PayloadSha256, SnapshotPath = snapshot,
                        PreStateSha256 = entry.PreStateSha256, TemporaryPath = temp
                    });
                }
                if (records.Count == 0) throw new InvalidDataException("Empty plan.");
                var intent = new Intent { Version = 1, TargetRoot = root, StateDirectory = state,
                    ProtectedPaths = protectedPaths.ToArray(), Entries = records.ToArray() };
                string intentPath = Path.Combine(state, "apply-" + Guid.NewGuid().ToString("N") + ".json");
                string intentTemp = intentPath + ".writing";
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(intent, ControlledFullZipIntentJsonContext.Default.Intent);
                using (var file = new FileStream(intentTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(json, 0, json.Length);
                    file.Flush(true);
                }
                File.Move(intentTemp, intentPath);
                afterIntent?.Invoke();
                for (int i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    CheckDirectories(Path.GetDirectoryName(record.TargetPath)!);
                    VerifyTarget(record.TargetPath, record.PreStateSha256);
                    using (var output = new FileStream(record.TemporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        var source = inputs[InputIndex(records, i)];
                        source.Position = 0;
                        source.CopyTo(output);
                        output.Flush(true);
                        Verify(output, record.PayloadSha256);
                    }
                    // This is a conflict check, not an atomic compare-and-swap.
                    VerifyTarget(record.TargetPath, record.PreStateSha256);
                    if (record.PreStateSha256 == null)
                        File.Move(record.TemporaryPath, record.TargetPath);
                    else
                        File.Replace(record.TemporaryPath, record.TargetPath, null);
                    using (var result = OpenRegular(record.TargetPath, false)) Verify(result, record.PayloadSha256);
                    afterWrite?.Invoke(i);
                }
                return intentPath;
            }
            finally
            {
                foreach (var input in inputs) input.Dispose();
            }
        }

        private static int InputIndex(List<IntentEntry> records, int index)
        {
            int result = 0;
            for (int i = 0; i < index; i++) result += records[i].PreStateSha256 == null ? 1 : 2;
            return result;
        }

        private static void VerifyTarget(string path, string? hash)
        {
            if (hash == null)
            {
                // GetAttributes detects directories and dangling reparse points as conflicts too.
                try { File.GetAttributes(path); }
                catch (FileNotFoundException) { return; }
                throw new IOException("Target already exists: " + path);
            }
            using (var stream = OpenRegular(path, false)) Verify(stream, hash);
        }

        internal static FileStream OpenRegular(string path, bool writable)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device |
                FileAttributes.Encrypted | FileAttributes.Offline |
                FileAttributes.SparseFile | FileAttributes.ReadOnly)) != 0)
                throw new InvalidDataException("Unsupported file: " + path);
            var stream = new FileStream(path, FileMode.Open, writable ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read);
            try
            {
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info) ||
                    info.NumberOfLinks != 1 || (info.FileAttributes & 0x400) != 0)
                    throw new IOException("Cannot verify regular unlinked file: " + path);
                CheckStreams(path);
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        private static void CheckStreams(string path)
        {
            IntPtr find = FindFirstStreamW(path, 0, out var data, 0);
            if (find == new IntPtr(-1)) throw new IOException("Cannot enumerate file streams: " + path);
            try
            {
                if (!string.Equals(data.Name, "::$DATA", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsupported file stream: " + path);
                if (FindNextStreamW(find, out data))
                    throw new InvalidDataException("Alternate streams are unsupported: " + path);
                if (Marshal.GetLastWin32Error() != 38)
                    throw new IOException("Cannot enumerate file streams: " + path);
            }
            finally { FindClose(find); }
        }

        private static void Verify(FileStream file, string expected)
        {
            file.Position = 0;
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("SHA-256 conflict: " + file.Name);
            }
        }

        private static void CheckHash(string value)
        {
            if (value == null || value.Length != 64) throw new InvalidDataException("Invalid SHA-256.");
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    throw new InvalidDataException("Invalid SHA-256.");
        }

        internal static string Relative(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.IndexOf('/') >= 0)
                throw new InvalidDataException("Invalid relative path.");
            foreach (string segment in path.Split('\\')) CheckSegment(segment);
            return path;
        }

        internal static string Absolute(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 3 || !char.IsLetter(path[0]) ||
                path[1] != ':' || path[2] != '\\' || path.IndexOf('/') >= 0)
                throw new InvalidDataException("A local absolute path is required.");
            string trimmed = path.TrimEnd('\\');
            if (trimmed.Length == 2) trimmed += "\\";
            if (!string.Equals(Path.GetFullPath(trimmed), trimmed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Non-canonical path.");
            var drive = new DriveInfo(trimmed.Substring(0, 3));
            if (drive.DriveType != DriveType.Fixed ||
                !string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only local fixed NTFS volumes are supported.");
            foreach (string segment in trimmed.Substring(3).Split('\\'))
                if (segment.Length != 0) CheckSegment(segment);
            return trimmed;
        }

        private static void CheckSegment(string segment)
        {
            if (segment.Length == 0 || segment == "." || segment == ".." ||
                segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal) ||
                segment.IndexOf('~') >= 0)
                throw new InvalidDataException("Invalid path component.");
            foreach (char c in segment)
                if (c < 32 || "<>:\"|?*".IndexOf(c) >= 0) throw new InvalidDataException("Invalid path component.");
            string stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                stem == "CONIN$" || stem == "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                ("123456789¹²³".IndexOf(stem[3]) >= 0)))
                throw new InvalidDataException("Reserved path component.");
        }

        internal static bool Overlaps(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith(b.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                b.StartsWith(a.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        internal static void CheckDirectories(string path)
        {
            string? current = path;
            while (current != null)
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.Directory) == 0 ||
                    (attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    throw new InvalidDataException("Unsafe directory: " + current);
                current = Path.GetDirectoryName(current);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(string name, int level, out StreamData data, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextStreamW(IntPtr find, out StreamData data);
        [DllImport("kernel32.dll")]
        private static extern bool FindClose(IntPtr find);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StreamData
        {
            public long Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
            public string Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
            public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
        }

        internal sealed class Intent
        {
            public int Version { get; set; }
            public string TargetRoot { get; set; } = "";
            public string StateDirectory { get; set; } = "";
            public string[] ProtectedPaths { get; set; } = Array.Empty<string>();
            public IntentEntry[] Entries { get; set; } = Array.Empty<IntentEntry>();
        }

        internal sealed class IntentEntry
        {
            [JsonPropertyName("path")]
            public string RelativePath { get; set; } = "";
            public string TargetPath { get; set; } = "";
            public string StagedPath { get; set; } = "";
            [JsonPropertyName("updatedSha256")]
            public string PayloadSha256 { get; set; } = "";
            public string? SnapshotPath { get; set; }
            [JsonPropertyName("originalSha256")]
            public string? PreStateSha256 { get; set; }
            public string TemporaryPath { get; set; } = "";
        }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ControlledFullZipApply.Intent))]
    internal partial class ControlledFullZipIntentJsonContext : JsonSerializerContext
    {
    }
}
