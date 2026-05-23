using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Differential.Abstractions;

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
    /// <para>
    /// An optional <see cref="IBinaryDiffer"/> can be supplied to customise the binary diff
    /// algorithm.  Defaults to <see cref="Differ.StreamingHdiffDiffer"/> with Deflate compression
    /// for optimal patch size and speed.
    /// </para>
    /// </summary>
    public class DefaultCleanStrategy : ICleanStrategy
    {
        private const string PatchFormat = ".patch";
        private const string DeleteFilesName = "generalupdate_delete_files.json";

        private readonly ICleanMatcher _matcher;
        private readonly IBinaryDiffer _binaryDiffer;

        /// <summary>
        /// Initialises a new instance using StreamingHdiffDiffer with Deflate compression by default.
        /// </summary>
        public DefaultCleanStrategy()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initialises a new instance, optionally using a custom file-matching strategy
        /// and/or a custom binary differ.
        /// If no matcher is provided, <see cref="DefaultCleanMatcher"/> is used.
        /// If no binary differ is provided, <see cref="Differ.StreamingHdiffDiffer"/> (Deflate) is used.
        /// </summary>
        public DefaultCleanStrategy(ICleanMatcher? matcher = null, IBinaryDiffer? binaryDiffer = null)
        {
            _matcher = matcher ?? new DefaultCleanMatcher();
            _binaryDiffer = binaryDiffer ?? new Differ.StreamingHdiffDiffer();
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
                        await _binaryDiffer.CleanAsync(oldFile.FullName, newFile.FullName, tempPatchPath);
                    }
                }
                else
                {
                    File.Copy(newFile.FullName, Path.Combine(tempDir, Path.GetFileName(newFile.FullName)), true);
                }
            }

            var exceptFiles = _matcher.Except(sourcePath, targetPath)?.ToList();
            if (exceptFiles is not null && exceptFiles.Any())
            {
                var path = Path.Combine(patchPath, DeleteFilesName);
                StorageManager.CreateJson(path, exceptFiles, FileNodesJsonContext.Default.ListFileNode);
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
