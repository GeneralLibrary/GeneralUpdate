using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Default implementation of <see cref="IDirtyStrategy"/> for the Dirty (patch-application) phase.
/// </summary>
/// <remarks>
/// An optional <see cref="IDirtyMatcher"/> can be supplied to customise how an
/// application file is matched to its corresponding patch file.
/// An optional <see cref="IBinaryDiffer"/> can be supplied to customise the binary diff
/// algorithm. Defaults to <see cref="GeneralUpdate.Differential.Differ.StreamingHdiffDiffer"/> with Deflate compression.
///
/// File replacement strategy: after patching, the strategy performs an atomic
/// delete-and-replace of the original file with the patched output.
/// This ensures correctness regardless of the IBinaryDiffer implementation used.
/// </remarks>
public class DefaultDirtyStrategy : IDirtyStrategy
{
    private const string DeleteFilesName = "generalupdate_delete_files.json";

    private readonly IDirtyMatcher _matcher;
    private readonly IBinaryDiffer _binaryDiffer;

    /// <summary>
    /// Initialises a new instance using StreamingHdiffDiffer with Deflate compression by default.
    /// </summary>
    public DefaultDirtyStrategy()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initialises a new instance, optionally using a custom file-matching strategy
    /// and/or a custom binary differ.
    /// </summary>
    public DefaultDirtyStrategy(IDirtyMatcher? matcher = null, IBinaryDiffer? binaryDiffer = null)
    {
        _matcher = matcher ?? new DefaultDirtyMatcher();
        _binaryDiffer = binaryDiffer ?? new GeneralUpdate.Differential.Differ.StreamingHdiffDiffer();
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(string appPath, string patchPath)
    {
        if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

        var skipDirectory = BlackListDefaults.DefaultSkipDirectories;
        var patchFiles = StorageManager.GetAllFiles(patchPath, skipDirectory);
        var oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory);
        HandleDeleteList(patchFiles, oldFiles);
        oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory);
        foreach (var oldFile in oldFiles)
        {
            var findFile = _matcher.Match(oldFile, patchFiles);
            if (findFile != null)
                await ApplyPatch(oldFile.FullName, findFile.FullName);
        }

        await CopyUnknownFiles(appPath, patchPath);
    }

    private static void HandleDeleteList(IEnumerable<FileInfo> patchFiles, IEnumerable<FileInfo> oldFiles)
    {
        var json = patchFiles.FirstOrDefault(i => i.Name.Equals(DeleteFilesName));
        if (json == null) return;

        var deleteFiles = StorageManager.GetJson<List<FileNode>>(json.FullName, FileNodesJsonContext.Default.ListFileNode);
        if (deleteFiles == null) return;

        var hashAlgorithm = new Sha256HashAlgorithm();
        var toDelete = oldFiles
            .Where(old => deleteFiles.Any(del => del.Hash.SequenceEqual(hashAlgorithm.ComputeHash(old.FullName))))
            .ToList();

        foreach (var file in toDelete)
        {
            if (!File.Exists(file.FullName)) continue;
            File.SetAttributes(file.FullName, FileAttributes.Normal);
            File.Delete(file.FullName);
        }
    }

    /// <summary>
    /// Applies a patch to a single file, then atomically replaces the original with the patched version.
    /// The IBinaryDiffer writes to a temp path; this method handles the replacement.
    /// </summary>
    private async Task ApplyPatch(string appFilePath, string patchFilePath)
    {
        if (!File.Exists(appFilePath) || !File.Exists(patchFilePath)) return;

        var tempPath = Path.Combine(
            Path.GetDirectoryName(appFilePath)!,
            $"{Path.GetRandomFileName()}_{Path.GetFileName(appFilePath)}");

        await _binaryDiffer.DirtyAsync(appFilePath, tempPath, patchFilePath);

        if (File.Exists(tempPath) && !File.Exists(appFilePath))
        {
            File.Move(tempPath, appFilePath);
        }
        else if (File.Exists(tempPath) && File.Exists(appFilePath))
        {
            File.SetAttributes(appFilePath, FileAttributes.Normal);
            File.Delete(appFilePath);
            File.Move(tempPath, appFilePath);
        }
    }

    private static async Task CopyUnknownFiles(string appPath, string patchPath)
    {
        await Task.Run(() =>
        {
            var fileManager = new StorageManager();
            var comparisonResult = fileManager.Compare(appPath, patchPath);
            foreach (var file in comparisonResult.DifferentNodes)
            {
                var extensionName = Path.GetExtension(file.FullName);
                if (BlackListDefaults.DefaultBlackFormats.Contains(extensionName)) continue;

                var targetFileName = file.FullName.Replace(patchPath, "").TrimStart(Path.DirectorySeparatorChar);
                var targetPath = Path.Combine(appPath, targetFileName);
                var parentFolder = Directory.GetParent(targetPath);
                if (parentFolder?.Exists == false)
                    parentFolder.Create();

                File.Copy(file.FullName, targetPath, true);
            }

            if (Directory.Exists(patchPath))
                StorageManager.DeleteDirectory(patchPath);
        });
    }
}
