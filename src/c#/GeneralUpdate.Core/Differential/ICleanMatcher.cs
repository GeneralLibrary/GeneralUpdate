using System.Collections.Generic;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Defines the complete matching strategy interface for the Clean (differential generation) stage.
/// Implementations are responsible for directory comparison, identifying deleted files,
/// and matching new files to their corresponding old files.
/// </summary>
/// <remarks>
/// <para>
/// The Clean stage (differential generation stage) is the first step of differential updates.
/// Its core objective is to extract all information needed to generate differential patches
/// from two versions of a directory. This interface defines three key operations:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Compare"/>: Recursively compares the old and new directories
///   to obtain complete differential information.</description></item>
///   <item><description><see cref="Except"/>: Identifies files that existed in the old version
///   but were removed in the new version.</description></item>
///   <item><description><see cref="Match"/>: Establishes a correspondence between files in the
///   new version and their counterparts in the old version, enabling subsequent binary
///   patch generation via a differential algorithm.</description></item>
/// </list>
/// <para>
/// Refer to <see cref="DefaultCleanMatcher"/> for the default implementation.
/// Implement this interface directly if you need custom matching logic
/// (e.g., hash-based or custom metadata-based matching).
/// </para>
/// </remarks>
public interface ICleanMatcher
{
    /// <summary>
    /// Compares the source and target directories and returns the collection of files
    /// that have changed.
    /// </summary>
    /// <param name="sourcePath">The source (old version) directory path.</param>
    /// <param name="targetPath">The target (new version) directory path.</param>
    /// <returns>A <see cref="ComparisonResult"/> containing the left and right node lists
    /// and the list of differing nodes.</returns>
    /// <remarks>
    /// This is the entry point for differential generation. Implementations should recursively
    /// traverse both directories and identify all added, modified, and deleted files
    /// by comparing file hashes or metadata.
    /// </remarks>
    ComparisonResult Compare(string sourcePath, string targetPath);

    /// <summary>
    /// Returns files that exist only in the source directory
    /// (i.e., files deleted in the new version).
    /// </summary>
    /// <param name="sourcePath">The source (old version) directory path.</param>
    /// <param name="targetPath">The target (new version) directory path.</param>
    /// <returns>An enumerable collection of <see cref="FileNode"/> instances that exist only
    /// in the source directory; an empty collection if there are no differences.</returns>
    /// <remarks>
    /// The results of this method are used to generate "delete" instructions in the update package,
    /// telling the client to remove these files when applying the update.
    /// </remarks>
    IEnumerable<FileNode>? Except(string sourcePath, string targetPath);

    /// <summary>
    /// Attempts to find the old file node corresponding to a specified new file
    /// from the source (left) node collection.
    /// </summary>
    /// <param name="newFile">The new file node from the target directory.</param>
    /// <param name="leftNodes">The collection of all file nodes from the source directory.</param>
    /// <returns>
    /// The matched old <see cref="FileNode"/>; or <c>null</c> if no match is found
    /// (indicating the file is entirely new and should be copied directly rather than
    /// having a differential patch generated).
    /// </returns>
    /// <remarks>
    /// For successfully matched files, a differential algorithm (e.g., bsdiff) will be used
    /// to generate a binary patch. For unmatched new files, the complete file content will
    /// be packaged into the update package.
    /// Implementations should verify both the file name and relative path to ensure
    /// matching accuracy.
    /// </remarks>
    FileNode? Match(FileNode newFile, IEnumerable<FileNode> leftNodes);
}
