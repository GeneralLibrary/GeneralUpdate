using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Default implementation of <see cref="ICleanMatcher"/> that preserves the
    /// original matching behaviour of <c>DifferentialCore.Clean</c>:
    /// a new file matches an old file when both share the same name, both exist
    /// on disk, and they reside at the same relative path.
    /// </summary>
    public class DefaultCleanMatcher : ICleanMatcher
    {
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
