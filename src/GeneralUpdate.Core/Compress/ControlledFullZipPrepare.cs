using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace GeneralUpdate.Core.Compress
{
    // The caller must hold externally enforced exclusive access and protect all roots.
    // Preparing a plan does not authenticate a controller or authorize installation writes.
    internal static class ControlledFullZipPrepare
    {
        internal static ControlledFullZipPlan Prepare(StagedZip staged, string targetRoot,
            string stateDirectory, string backupRoot, IEnumerable<string> protectedPaths)
        {
            if (staged == null) throw new ArgumentNullException(nameof(staged));
            if (protectedPaths == null) throw new ArgumentNullException(nameof(protectedPaths));
            if (Path.DirectorySeparatorChar != '\\') throw new PlatformNotSupportedException("Windows only.");
            string target = ControlledFullZipApply.Absolute(targetRoot);
            string state = ControlledFullZipApply.Absolute(stateDirectory);
            string backup = ControlledFullZipApply.Absolute(backupRoot);
            string stage = ControlledFullZipApply.Absolute(staged.DirectoryPath);
            var roots = new[] { target, state, backup, stage };
            for (int i = 0; i < roots.Length; i++)
            {
                ControlledFullZipApply.CheckDirectories(roots[i]);
                for (int j = 0; j < i; j++)
                    if (ControlledFullZipApply.Overlaps(roots[i], roots[j]))
                        throw new InvalidDataException("Controlled roots overlap.");
            }
            if (Directory.GetFileSystemEntries(backup).Length != 0)
                throw new IOException("Backup directory must be empty.");

            var protectedNames = new List<string>();
            foreach (string value in protectedPaths)
                protectedNames.Add(ControlledFullZipApply.Relative(value));
            var names = new List<string>();
            var originals = new List<FileStream?>();
            try
            {
                foreach (var entry in staged.Entries)
                {
                    string relative = ControlledFullZipApply.Relative(entry.RelativePath.Replace('/', '\\'));
                    foreach (string name in names)
                        if (ControlledFullZipApply.Overlaps(name, relative))
                            throw new InvalidDataException("Conflicting target paths.");
                    foreach (string name in protectedNames)
                        if (ControlledFullZipApply.Overlaps(name, relative))
                            throw new InvalidDataException("Protected target path.");
                    if (ControlledFullZipApply.Overlaps(".backups", relative))
                        throw new InvalidDataException("Protected target path.");
                    string stagedPath = ControlledFullZipApply.Absolute(entry.StagedPath);
                    if (!stagedPath.StartsWith(stage.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Staged file is outside the stage directory.");
                    ControlledFullZipApply.CheckDirectories(Path.GetDirectoryName(stagedPath)!);
                    using (ControlledFullZipApply.OpenRegular(stagedPath, false)) { }
                    string destination = Path.Combine(target, relative);
                    ControlledFullZipApply.CheckDirectories(Path.GetDirectoryName(destination)!);
                    FileStream? original;
                    try { original = ControlledFullZipApply.OpenRegular(destination, false); }
                    catch (FileNotFoundException) { original = null; }
                    originals.Add(original);
                    names.Add(relative);
                }
                if (names.Count == 0) throw new InvalidDataException("Empty plan.");

                var entries = new List<ControlledFullZipEntry>();
                for (int i = 0; i < names.Count; i++)
                {
                    string? snapshot = null;
                    string? hash = null;
                    if (originals[i] != null)
                    {
                        snapshot = Path.Combine(backup, names[i]);
                        string parent = Path.GetDirectoryName(snapshot)!;
                        Directory.CreateDirectory(parent);
                        ControlledFullZipApply.CheckDirectories(parent);
                        using (var output = new FileStream(snapshot, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                        {
                            originals[i]!.CopyTo(output);
                            output.Flush(true);
                            output.Position = 0;
                            using (var sha = SHA256.Create())
                                hash = BitConverter.ToString(sha.ComputeHash(output)).Replace("-", "").ToLowerInvariant();
                        }
                    }
                    entries.Add(new ControlledFullZipEntry(names[i], staged.Entries[i].StagedPath,
                        staged.Entries[i].Sha256, snapshot, hash));
                }
                return new ControlledFullZipPlan(target, state, entries, protectedNames);
            }
            finally
            {
                foreach (var stream in originals) stream?.Dispose();
            }
        }
    }
}
