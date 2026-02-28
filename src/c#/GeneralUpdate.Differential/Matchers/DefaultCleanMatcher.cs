using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Default implementation of <see cref="ICleanMatcher"/> that preserves the
    /// original behaviour of <c>DifferentialCore.Clean</c>.
    /// <list type="bullet">
    ///   <item><description><see cref="Compare"/> delegates to <see cref="StorageManager.Compare"/>.</description></item>
    ///   <item><description><see cref="Except"/> delegates to <see cref="StorageManager.Except"/>.</description></item>
    ///   <item><description><see cref="Match"/> considers a new file matched to an old file when both share the
    ///   same name, both exist on disk, and they reside at the same relative path.</description></item>
    /// </list>
    /// </summary>
    public class DefaultCleanMatcher : ICleanMatcher
    {
        private readonly StorageManager _storageManager = new StorageManager();

        /// <inheritdoc/>
        public ComparisonResult Compare(string sourcePath, string targetPath)
            => _storageManager.Compare(sourcePath, targetPath);

        /// <inheritdoc/>
        public IEnumerable<FileNode>? Except(string sourcePath, string targetPath)
            => _storageManager.Except(sourcePath, targetPath);

        /// <inheritdoc/>
        public FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes)
        {
            var oldFile = leftNodes.FirstOrDefault(i => i.Name.Equals(newFile.Name));
            if (oldFile is null) return null;
            if (!File.Exists(oldFile.FullName)) return null;
            if (!File.Exists(newFile.FullName)) return null;
            if (!string.Equals(oldFile.RelativePath, newFile.RelativePath)) return null;
            return oldFile;
        }
    }
}
