namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Defines the blacklist matcher interface for excluding specific files and directories from traversal.
/// </summary>
/// <remarks>
/// <para>
/// This interface is the core abstraction for the GeneralUpdate file system filtering layer,
/// allowing implementers to define custom rules for excluding files and directories from file traversal.
/// It is primarily used in:
/// </para>
/// <list type="bullet">
///   <item><description>The <see cref="StorageManager.ReadFileNode"/> method to filter files during file system traversal.</description></item>
///   <item><description>The <see cref="FileTreeEnumerator"/> to apply blacklist rules during file enumeration.</description></item>
///   <item><description>Backup and differential update scenarios to skip files that do not need processing.</description></item>
/// </list>
/// <para>
/// For the default implementation, refer to <see cref="BlackMatcher"/>.
/// </para>
/// </remarks>
public interface IBlackMatcher
{
    /// <summary>
    /// Determines whether the specified file should be excluded by the blacklist.
    /// </summary>
    /// <param name="relativeFilePath">The relative path or file name of the file.</param>
    /// <returns><c>true</c> if the file should be excluded; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Implementations should typically extract the file name and extension for matching checks.
    /// This method is called for every file encountered, so implementations should remain efficient.
    /// </remarks>
    bool IsBlacklisted(string relativeFilePath);

    /// <summary>
    /// Determines whether the specified file extension is in the blacklist.
    /// </summary>
    /// <param name="extension">The file extension (including leading dot, e.g., <c>.log</c>).</param>
    /// <returns><c>true</c> if the extension is in the blacklist; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is specifically designed for quick checks against file format/type-level blacklist rules.
    /// </remarks>
    bool IsBlacklistedFormat(string extension);

    /// <summary>
    /// Determines whether the specified directory name should be skipped during file traversal.
    /// </summary>
    /// <param name="directoryName">The directory name.</param>
    /// <returns><c>true</c> if the directory should be skipped; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// When this method returns <c>true</c>, the traverser will not enter the directory or its subdirectories.
    /// </remarks>
    bool ShouldSkipDirectory(string directoryName);
}
