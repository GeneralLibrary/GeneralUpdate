using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Default implementation of <see cref="ICleanMatcher"/> that preserves the original behavior
/// of the Clean stage (differential generation).
/// </summary>
/// <remarks>
/// <para>
/// DefaultCleanMatcher implements three core matching operations for the differential
/// generation stage:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Compare"/>: Delegates to <see cref="StorageManager.Compare"/>,
///   performing a full recursive comparison of two directories.</description></item>
///   <item><description><see cref="Except"/>: Delegates to <see cref="StorageManager.Except"/>,
///   identifying files present in the source but absent in the target
///   (i.e., files to be deleted).</description></item>
///   <item><description><see cref="Match"/>: Performs a case-insensitive match on file name and
///   relative path to find the corresponding old version of a new file.
///   Both files must exist on disk for the match to be considered valid.</description></item>
/// </list>
/// <para>
/// This implementation is designed to maintain backward compatibility with earlier versions.
/// To customize matching logic (e.g., hash-based or metadata-based matching),
/// implement the <see cref="ICleanMatcher"/> interface directly.
/// </para>
/// </remarks>
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
        var oldFile = leftNodes.FirstOrDefault(i =>
            string.Equals(i.Name, newFile.Name, System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.RelativePath, newFile.RelativePath, System.StringComparison.OrdinalIgnoreCase));
        if (oldFile is null) return null;
        if (!File.Exists(oldFile.FullName)) return null;
        if (!File.Exists(newFile.FullName)) return null;
        return oldFile;
    }
}
