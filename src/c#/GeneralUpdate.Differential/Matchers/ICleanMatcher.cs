using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Defines the complete matching strategy used during the Clean (diff-generation) phase.
    /// Implementations are responsible for directory comparison, identifying deleted files,
    /// and matching individual new files to their corresponding old files.
    /// </summary>
    public interface ICleanMatcher
    {
        /// <summary>
        /// Compares the source and target directories and returns the set of changed files.
        /// </summary>
        /// <param name="sourcePath">The source (old-version) directory.</param>
        /// <param name="targetPath">The target (new-version) directory.</param>
        ComparisonResult Compare(string sourcePath, string targetPath);

        /// <summary>
        /// Returns the files that exist only in the source directory (i.e. files to be deleted).
        /// </summary>
        /// <param name="sourcePath">The source (old-version) directory.</param>
        /// <param name="targetPath">The target (new-version) directory.</param>
        IEnumerable<FileNode>? Except(string sourcePath, string targetPath);

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
