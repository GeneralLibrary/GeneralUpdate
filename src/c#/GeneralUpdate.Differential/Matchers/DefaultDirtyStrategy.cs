using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.HashAlgorithms;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Differential.Binary;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Default implementation of <see cref="IDirtyStrategy"/> that preserves the original
    /// behaviour of the Dirty (patch-application) phase.
    /// <para>
    /// An optional <see cref="IDirtyMatcher"/> can be supplied to customise how an
    /// application file is matched to its corresponding patch file, without replacing the
    /// entire execution flow.  When no matcher is provided, <see cref="DefaultDirtyMatcher"/>
    /// is used.
    /// </para>
    /// </summary>
    public class DefaultDirtyStrategy : IDirtyStrategy
    {
        private const string DeleteFilesName = "generalupdate_delete_files.json";

        private readonly IDirtyMatcher _matcher;

        /// <summary>
        /// Initialises a new instance, optionally using a custom file-matching strategy.
        /// If no matcher is provided, <see cref="DefaultDirtyMatcher"/> is used.
        /// </summary>
        public DefaultDirtyStrategy(IDirtyMatcher? matcher = null)
        {
            _matcher = matcher ?? new DefaultDirtyMatcher();
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string appPath, string patchPath)
        {
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

            var skipDirectory = BlackListManager.Instance.SkipDirectorys.ToList();
            var patchFiles = StorageManager.GetAllFiles(patchPath, skipDirectory);
            var oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory);
            // Refresh the collection after deleting the files.
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

        private static async Task ApplyPatch(string appFilePath, string patchFilePath)
        {
            if (!File.Exists(appFilePath) || !File.Exists(patchFilePath)) return;
            var newPath = Path.Combine(
                Path.GetDirectoryName(appFilePath)!,
                $"{Path.GetRandomFileName()}_{Path.GetFileName(appFilePath)}");
            await new BinaryHandler().Dirty(appFilePath, newPath, patchFilePath);
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
                    if (BlackListManager.Instance.IsBlacklisted(extensionName)) continue;

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
}
