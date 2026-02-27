using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Defines the matching logic used during the Clean (diff-generation) phase to
    /// find the corresponding old file for a given new file.
    /// </summary>
    public interface ICleanMatcher
    {
        /// <summary>
        /// Attempts to find the corresponding old file node for <paramref name="newFile"/>
        /// from the left-side (source) node collection.
        /// </summary>
        /// <param name="newFile">The new file node from the target directory.</param>
        /// <param name="leftNodes">All file nodes from the source directory.</param>
        /// <returns>
        /// The matching old <see cref="FileNode"/>, or <c>null</c> if no match is found
        /// (indicating the file is brand-new and should be copied directly).
        /// </returns>
        FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes);
    }
}
