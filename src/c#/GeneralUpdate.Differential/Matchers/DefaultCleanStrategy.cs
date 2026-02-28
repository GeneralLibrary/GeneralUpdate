using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Differential.Binary;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Default implementation of <see cref="ICleanStrategy"/> that preserves the original
    /// behaviour of the Clean (patch-generation) phase.
    /// <para>
    /// An optional <see cref="ICleanMatcher"/> can be supplied to customise the directory
    /// comparison, deleted-file detection, or per-file matching logic without replacing the
    /// entire execution flow.  When no matcher is provided, <see cref="DefaultCleanMatcher"/>
    /// is used.
    /// </para>
    /// </summary>
    public class DefaultCleanStrategy : ICleanStrategy
    {
        private const string PatchFormat = ".patch";
        private const string DeleteFilesName = "generalupdate_delete_files.json";

        private readonly ICleanMatcher _matcher;

        /// <summary>
        /// Initialises a new instance, optionally using a custom file-matching strategy.
        /// If no matcher is provided, <see cref="DefaultCleanMatcher"/> is used.
        /// </summary>
        public DefaultCleanStrategy(ICleanMatcher? matcher = null)
        {
            _matcher = matcher ?? new DefaultCleanMatcher();
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string sourcePath, string targetPath, string patchPath)
        {
            var comparisonResult = _matcher.Compare(sourcePath, targetPath);
            foreach (var file in comparisonResult.DifferentNodes)
            {
                var tempDir = GetTempDirectory(file, targetPath, patchPath);
                var oldFile = _matcher.Match(file, comparisonResult.LeftNodes);
                var newFile = file;

                if (oldFile is not null)
                {
                    if (!StorageManager.HashEquals(oldFile.FullName, newFile.FullName))
                    {
                        var tempPatchPath = Path.Combine(tempDir, $"{file.Name}{PatchFormat}");
                        await new BinaryHandler().Clean(oldFile.FullName, newFile.FullName, tempPatchPath);
                    }
                }
                else
                {
                    File.Copy(newFile.FullName, Path.Combine(tempDir, Path.GetFileName(newFile.FullName)), true);
                }
            }

            var exceptFiles = _matcher.Except(sourcePath, targetPath);
            if (exceptFiles is not null && exceptFiles.Any())
            {
                var path = Path.Combine(patchPath, DeleteFilesName);
                StorageManager.CreateJson(path, exceptFiles);
            }
        }

        private static string GetTempDirectory(FileNode file, string targetPath, string patchPath)
        {
            var tempPath = file.FullName.Replace(targetPath, "").Replace(Path.GetFileName(file.FullName), "").Trim(Path.DirectorySeparatorChar);
            var tempDir = string.IsNullOrEmpty(tempPath) ? patchPath : Path.Combine(patchPath, tempPath);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }
    }
}
