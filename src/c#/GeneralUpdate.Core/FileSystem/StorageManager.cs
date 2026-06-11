using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.FileSystem
{
    /// <summary>
    /// Storage manager providing static utility methods for file system operations.
    /// Supports backup, restore, directory comparison, file traversal, hash verification, and blacklist filtering.
    /// This class is the unified entry point for all file system operations and is responsible for
    /// generating version directory snapshots and performing difference comparisons during the update workflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StorageManager is the central hub for file operations within the entire update framework.
    /// Its primary responsibilities include:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>Backup</c> / <c>Restore</c>: Create and restore full application backups.</description></item>
    ///   <item><description><c>Compare</c>: Recursively compare two directories to identify added, modified, and deleted files.</description></item>
    ///   <item><description><c>HashEquals</c>: Verify whether two files are identical using the SHA-256 hash algorithm.</description></item>
    ///   <item><description><c>GetAllFiles</c>: Recursively retrieve all files under a specified directory, with support for skipping directories.</description></item>
    ///   <item><description><c>CleanBackup</c> / <c>ListBackups</c>: Manage historical backup versions with support for retaining the most recent N versions.</description></item>
    /// </list>
    /// <para>
    /// A blacklist matcher can be set via the <see cref="BlackMatcher"/> static property to exclude specific files or directories during file traversal.
    /// All public methods are thread-safe static methods; however, the <see cref="Compare"/> method uses instance state internally,
    /// so concurrent calls should be avoided on the same instance.
    /// </para>
    /// </remarks>
    public sealed class StorageManager
    {
        private long _fileCount = 0;

        /// <summary>
        /// The default prefix for backup directory names (e.g. "backup-20260606235200").
        /// </summary>
        public const string DirectoryName = "backup-";

        /// <summary>
        /// Legacy backup directory prefix used by older versions (e.g. "app-1.0.0").
        /// Retained for backward compatibility in discovery and cleanup.
        /// </summary>
        public const string LegacyDirectoryPrefix = "app-";

        /// <summary>
        /// The subdirectory under the install path where new-format backups are stored.
        /// </summary>
        public const string BackupRootDirectory = ".backups";

        /// <summary>
        /// Backup directory name prefixes used for enumeration (both new and legacy formats).
        /// Derived from <see cref="DirectoryName"/> and <see cref="LegacyDirectoryPrefix"/>.
        /// </summary>
        private static readonly string[] BackupNamePrefixes = { DirectoryName, LegacyDirectoryPrefix };

        /// <summary>
        /// Gets or sets the optional path/file blacklist matcher.
        /// </summary>
        /// <value>
        /// An instance implementing the <see cref="IBlackMatcher"/> interface, used to exclude blacklisted files or directories during file traversal.
        /// Must be set before any file operations are performed.
        /// </value>
        /// <remarks>
        /// When this property is set, the <see cref="ReadFileNode"/> method automatically skips files and directories that match the blacklist during file system traversal.
        /// Example of setting: <c>StorageManager.BlackMatcher = new BlackMatcher(config);</c>
        /// </remarks>
        public static IBlackMatcher? BlackMatcher { get; set; }
        
        private ComparisonResult ComparisonResult { get; set; }

        #region Public Methods

        /// <summary>
        /// Finds the set of files present in the left directory but not in the right directory (i.e. files that have been deleted).
        /// </summary>
        /// <param name="leftPath">The base (old version) directory path.</param>
        /// <param name="rightPath">The target (new version) directory path.</param>
        /// <returns>
        /// A collection of <see cref="FileNode"/> instances that exist in the left directory but not in the right directory;
        /// returns an empty collection if both file lists are identical.
        /// </returns>
        /// <remarks>
        /// This method serializes both the left and right directories into <see cref="FileNode"/> lists,
        /// then builds a hash table keyed by <c>RelativePath</c> to perform a set difference operation.
        /// It is suitable for identifying old files that need to be deleted in a differential update scenario.
        /// </remarks>
        /// </remarks>
        public IEnumerable<FileNode>? Except(string leftPath, string rightPath)
        {
            var leftFileNodes = ReadFileNode(leftPath);
            var rightFileNodes = ReadFileNode(rightPath);
            var rightNodeDic = rightFileNodes.ToDictionary(x => x.RelativePath);
            return leftFileNodes.Where(f => !rightNodeDic.ContainsKey(f.RelativePath)).ToList();
        }

        /// <summary>
        /// Compares two directories and identifies the files that differ between them.
        /// </summary>
        /// <param name="leftDir">The base (old version) directory path.</param>
        /// <param name="rightDir">The target (new version) directory path.</param>
        /// <returns>
        /// A <see cref="ComparisonResult"/> object containing collections of left nodes, right nodes, and differing nodes.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Comparison flow:
        /// <list type="number">
        ///   <item><description>Resets the internal file ID counter.</description></item>
        ///   <item><description>Recursively reads all file nodes from both directories, generating <see cref="FileNode"/> lists.</description></item>
        ///   <item><description>Constructs left and right <see cref="FileTree"/> binary search trees.</description></item>
        ///   <item><description>Recursively compares corresponding nodes of the two trees starting from the root, collecting nodes with differing hash values or names.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Note: This method uses the instance-level <c>ComparisonResult</c> state and should not be called concurrently on the same instance in a multi-threaded environment.
        /// </para>
        /// </remarks>
        public ComparisonResult Compare(string leftDir, string rightDir)
        {
            ResetId();
            ComparisonResult = new ComparisonResult();
            var leftFileNodes = ReadFileNode(leftDir);
            var rightFileNodes = ReadFileNode(rightDir);
            var leftTree = new FileTree(leftFileNodes);
            var rightTree = new FileTree(rightFileNodes);
            var differentTreeNode = new List<FileNode>();
            leftTree.Compare(leftTree.GetRoot(), rightTree.GetRoot(), ref differentTreeNode);
            ComparisonResult.AddToLeft(leftFileNodes);
            ComparisonResult.AddToRight(rightFileNodes);
            ComparisonResult.AddDifferent(differentTreeNode);
            return ComparisonResult;
        }

        /// <summary>
        /// Serializes an object to JSON and writes it to the specified path.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize. Must be a reference type.</typeparam>
        /// <param name="targetPath">The full path of the target JSON file.</param>
        /// <param name="obj">The object instance to serialize.</param>
        /// <param name="typeInfo">Optional JSON type info metadata for source generator serialization support.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="targetPath"/> does not contain a valid directory path.</exception>
        /// <remarks>
        /// If the directory of the target file does not exist, it will be created automatically.
        /// Supports source generator mode via <c>JsonTypeInfo</c>, which avoids runtime reflection in AOT compilation scenarios.
        /// </remarks>
        public static void CreateJson<T>(string targetPath, T obj, JsonTypeInfo<T>? typeInfo = null) where T : class
        {
            var folderPath = Path.GetDirectoryName(targetPath) ??
                             throw new ArgumentException("invalid path", nameof(targetPath));

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var jsonString = typeInfo != null ? JsonSerializer.Serialize(obj, typeInfo) : JsonSerializer.Serialize(obj);
            File.WriteAllText(targetPath, jsonString);
        }

        /// <summary>
        /// Reads a JSON file from the specified path and deserializes it into the specified type.
        /// </summary>
        /// <typeparam name="T">The target type for deserialization. Must be a reference type.</typeparam>
        /// <param name="path">The full path of the JSON file.</param>
        /// <param name="typeInfo">Optional JSON type info metadata for source generator deserialization support.</param>
        /// <returns>The deserialized object instance; returns <c>default</c> if the file does not exist.</returns>
        /// <remarks>
        /// If the file does not exist, no exception is thrown and <c>null</c> is returned.
        /// Supports source generator mode via <c>JsonTypeInfo</c>.
        /// </remarks>
        public static T? GetJson<T>(string path, JsonTypeInfo<T>? typeInfo = null) where T : class
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                if (typeInfo != null)
                {
                    return JsonSerializer.Deserialize(json, typeInfo);
                }
                return JsonSerializer.Deserialize<T>(json);
            }

            return default;
        }

        /// <summary>
        /// Creates a uniquely named subdirectory in the system temporary directory for storing temporary update files.
        /// </summary>
        /// <param name="name">A custom name identifying the purpose of the temporary directory.</param>
        /// <returns>The full path of the created temporary directory.</returns>
        /// <remarks>
        /// The directory naming format is <c>generalupdate_{timestamp}_{processId}_{name}</c>.
        /// If the directory already exists, it will not be recreated. The caller is responsible for cleaning up this directory when it is no longer needed.
        /// </remarks>
        public static string GetTempDirectory(string name)
        {
            var path = $"generalupdate_{DateTime.Now:yyyy-MM-dd-HHmmss-fff}_{System.Diagnostics.Process.GetCurrentProcess().Id}_{name}";
            var tempDir = Path.Combine(Path.GetTempPath(), path);
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            return tempDir;
        }

        /// <summary>
        /// Generates a timestamp-based backup directory name in the format "backup-{yyyyMMddHHmmss}".
        /// </summary>
        /// <returns>A backup directory name string, e.g. "backup-20260606235200".</returns>
        /// <remarks>
        /// Timestamp naming ensures each backup is unique and naturally sortable by creation time.
        /// Used by <see cref="Backup"/> to create version-independent backup directory names.
        /// </remarks>
        public static string GetBackupDirectoryName() => $"{DirectoryName}{DateTime.Now:yyyyMMddHHmmss}";

        /// <summary>
        /// Finds the most recent backup directory by scanning for backup directories
        /// matching patterns derived from <see cref="BlackDefaults.DefaultDirectories"/>.
        /// </summary>
        /// <param name="installPath">The application installation root directory.</param>
        /// <returns>The full path of the most recent backup directory, or <c>null</c> if none exists.</returns>
        /// <remarks>
        /// Sorts candidates by directory creation time descending (most recent first), with
        /// a name-based tie-breaker for deterministic results. Unlike lexicographic name
        /// sorting, this correctly handles mixed paths (e.g. .backups/ vs installPath/)
        /// and legacy version-format names ("app-10.0.0" vs "app-2.0.0").
        /// </remarks>
        public static string? GetLatestBackup(string installPath)
        {
            if (!Directory.Exists(installPath)) return null;

            var allBackups = new List<DirectoryInfo>();

            // Scan BackupRootDirectory subdirectory (new-format backup container)
            var backupRoot = Path.Combine(installPath, BackupRootDirectory);
            if (Directory.Exists(backupRoot))
            {
                allBackups.AddRange(new DirectoryInfo(backupRoot)
                    .GetDirectories("*", SearchOption.TopDirectoryOnly));
            }

            // Scan installPath directly for backup dirs matching patterns from defaults
            allBackups.AddRange(GetBackupDirectoryInfos(installPath));

            return allBackups
                .OrderByDescending(d => d.CreationTime)
                .ThenByDescending(d => d.Name)
                .FirstOrDefault()?.FullName;
        }

        /// <summary>
        /// Enumerates all backup directories in the given path using patterns derived
        /// from <see cref="BackupNamePrefixes"/> (both new and legacy formats).
        /// </summary>
        private static IEnumerable<DirectoryInfo> GetBackupDirectoryInfos(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var prefix in BackupNamePrefixes)
            {
                foreach (var dir in dirInfo.GetDirectories(prefix + "*", SearchOption.TopDirectoryOnly))
                {
                    yield return dir;
                }
            }
        }

        /// <summary>
        /// Recursively deletes the specified directory and all of its subdirectories and files.
        /// </summary>
        /// <param name="targetDir">The path to the target directory to delete.</param>
        /// <remarks>
        /// Before deletion, each file's attributes are reset to <see cref="FileAttributes.Normal"/>
        /// to prevent deletion failures caused by read-only attributes. This operation is irreversible, use with caution.
        /// </remarks>
        public static void DeleteDirectory(string targetDir)
        {
            foreach (var file in Directory.GetFiles(targetDir))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(targetDir))
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDir, false);
        }

        /// <summary>
        /// Recursively retrieves all files under the specified directory, with support for skipping subdirectories via the blacklist.
        /// </summary>
        /// <param name="path">The root directory path to traverse.</param>
        /// <param name="skipDirectorys">The list of subdirectory names to skip (uses containment matching).</param>
        /// <returns>A collection of <see cref="FileInfo"/> instances for all files that were not skipped.</returns>
        /// <remarks>
        /// This method only skips first-level subdirectories (does not recursively skip) and is suitable for backup and full file enumeration scenarios.
        /// If an exception occurs during traversal (e.g., due to permissions), an empty collection is returned instead of throwing an exception.
        /// </remarks>
        public static List<FileInfo> GetAllFiles(string path, List<string> skipDirectorys)
        {
            try
            {
                var files = new List<FileInfo>();
                files.AddRange(new DirectoryInfo(path).GetFiles());
                var tmpDir = new DirectoryInfo(path).GetDirectories();

                foreach (var dic in tmpDir)
                {
                    bool shouldSkip = false;
                    foreach (var notBackup in skipDirectorys)
                    {
                        if (dic.FullName.Contains(notBackup))
                        {
                            shouldSkip = true;
                            break;
                        }
                    }

                    if (!shouldSkip)
                        files.AddRange(GetAllfiles(dic.FullName));
                }

                return files;
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"StorageManager.GetAllFiles failed for path '{path}': {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// Private recursive method that retrieves all files under the specified path (without blacklist filtering).
        /// </summary>
        /// <param name="path">The directory path to traverse.</param>
        /// <returns>A collection of <see cref="FileInfo"/> instances for all files in the directory.</returns>
        /// <remarks>
        /// Unlike <see cref="GetAllFiles"/>, this method does not include directory-skipping logic.
        /// If an exception occurs during traversal (e.g., due to permissions), an empty collection is returned instead of throwing an exception.
        /// </remarks>
        private static List<FileInfo> GetAllfiles(string path)
        {
            try
            {
                var files = new List<FileInfo>();
                files.AddRange(new DirectoryInfo(path).GetFiles());
                var tmpDir = new DirectoryInfo(path).GetDirectories();
                foreach (var dic in tmpDir)
                {
                    files.AddRange(GetAllfiles(dic.FullName));
                }

                return files;
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"StorageManager.GetAllfiles failed for path '{path}': {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// Compares the contents of two files to determine whether they are identical using the SHA-256 hash algorithm.
        /// </summary>
        /// <param name="leftPath">The full path of the first file.</param>
        /// <param name="rightPath">The full path of the second file.</param>
        /// <returns><c>true</c> if the hash values of the two files are equal; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method computes the SHA-256 hash of both files and compares the resulting byte sequences.
        /// It is suitable for large file comparisons and is more efficient than byte-by-byte reading.
        /// </remarks>
        public static bool HashEquals(string leftPath, string rightPath)
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashLeft = hashAlgorithm.ComputeHash(leftPath);
            var hashRight = hashAlgorithm.ComputeHash(rightPath);
            return hashLeft.SequenceEqual(hashRight);
        }

        /// <summary>
        /// Backs up the entire application directory to the specified location.
        /// </summary>
        /// <param name="sourcePath">The source application directory path.</param>
        /// <param name="backupPath">The target backup directory path.</param>
        /// <param name="directoryNames">The list of subdirectory names to skip (uses containment matching).</param>
        /// <remarks>
        /// <para>
        /// Backup flow:
        /// <list type="number">
        ///   <item><description>If the backup directory already exists, delete it first.</description></item>
        ///   <item><description>Create a new backup directory.</description></item>
        ///   <item><description>Recursively copy all files and subdirectories from the source directory, skipping directories that match <paramref name="directoryNames"/>.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// This method overwrites existing files in the target directory.
        /// </para>
        /// </remarks>
        public static void Backup(string sourcePath, string backupPath, IReadOnlyList<string> directoryNames)
        {
            // Merge default backup-exclusion prefixes with user-configured directories.
            // This ensures backup/legacy directories are ALWAYS skipped, preventing
            // infinite recursion even when the user passes an empty skip list.
            var effectiveDirectories = new List<string>(directoryNames);
            effectiveDirectories.AddRange(BlackDefaults.DefaultDirectories);

            if (Directory.Exists(backupPath))
            {
                DeleteDirectory(backupPath);
            }
            Directory.CreateDirectory(backupPath);
            CopyDirectory(sourcePath, backupPath, effectiveDirectories);
        }

        private static void CopyDirectory(string sourceDir, string targetDir, IReadOnlyList<string> directoryNames)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                var dirName = Path.GetFileName(dirPath);
                if (!directoryNames.Any(name => dirName.Contains(name)))
                {
                    string newTargetDir = Path.Combine(targetDir, Path.GetFileName(dirPath));
                    Directory.CreateDirectory(newTargetDir);
                    CopyDirectory(dirPath, newTargetDir, directoryNames);
                }
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string newFilePath = Path.Combine(targetDir, Path.GetFileName(filePath));
                File.Copy(filePath, newFilePath, true);
            }
        }

        /// <summary>
        /// Restores the entire application from a backup directory to the specified location.
        /// </summary>
        /// <param name="backupPath">The backup directory path.</param>
        /// <param name="sourcePath">The target application directory path to restore to.</param>
        /// <remarks>
        /// If the target directory does not exist, it will be created automatically. The restore operation copies all files and subdirectories
        /// from the backup directory to the target location, overwriting any existing files with the same name.
        /// This method does not include blacklist filtering logic and restores all backup content completely.
        /// </remarks>
        public static void Restore(string backupPath, string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }

            CopyDirectory(backupPath, sourcePath);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                string newTargetDir = Path.Combine(targetDir, Path.GetFileName(dirPath));
                Directory.CreateDirectory(newTargetDir);
                CopyDirectory(dirPath, newTargetDir);
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string newFilePath = Path.Combine(targetDir, Path.GetFileName(filePath));
                File.Copy(filePath, newFilePath, true);
            }
        }

        /// <summary>
        /// Asynchronously backs up the entire application directory (offloads the synchronous backup operation to the thread pool).
        /// </summary>
        /// <param name="sourcePath">The source application directory path.</param>
        /// <param name="backupPath">The target backup directory path.</param>
        /// <param name="directoryNames">The list of subdirectory names to skip.</param>
        /// <returns>A task representing the asynchronous backup operation.</returns>
        /// <remarks>
        /// This method offloads the <see cref="Backup"/> call to the thread pool via <c>Task.Run</c>,
        /// making it suitable for UI applications where blocking the main thread should be avoided.
        /// </remarks>
        public static async System.Threading.Tasks.Task BackupAsync(string sourcePath, string backupPath, System.Collections.Generic.IReadOnlyList<string> directoryNames)
        {
            await System.Threading.Tasks.Task.Run(() => Backup(sourcePath, backupPath, directoryNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously restores the application from a backup directory (offloads the synchronous restore operation to the thread pool).
        /// </summary>
        /// <param name="backupPath">The backup directory path.</param>
        /// <param name="sourcePath">The target application directory path to restore to.</param>
        /// <returns>A task representing the asynchronous restore operation.</returns>
        /// <remarks>
        /// This method offloads the <see cref="Restore"/> call to the thread pool via <c>Task.Run</c>.
        /// </remarks>
        public static async System.Threading.Tasks.Task RestoreAsync(string backupPath, string sourcePath)
        {
            await System.Threading.Tasks.Task.Run(() => Restore(backupPath, sourcePath)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously cleans up old backup versions, retaining only the most recent N versions (offloads the synchronous cleanup to the thread pool).
        /// </summary>
        /// <param name="installPath">The application installation root directory path.</param>
        /// <param name="keepVersions">The number of most recent backup versions to retain. Default is 3.</param>
        /// <returns>A task representing the asynchronous cleanup operation.</returns>
        /// <remarks>
        /// This method offloads the <see cref="CleanBackup"/> call to the thread pool via <c>Task.Run</c>.
        /// </remarks>
        public static async System.Threading.Tasks.Task CleanBackupAsync(string installPath, int keepVersions = 3)
        {
            await System.Threading.Tasks.Task.Run(() => CleanBackup(installPath, keepVersions)).ConfigureAwait(false);
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Private recursive method that reads all files under the specified directory and converts them into a list of <see cref="FileNode"/> instances.
        /// </summary>
        /// <param name="path">The directory path currently being traversed.</param>
        /// <param name="rootPath">The root directory path used for calculating relative paths. If <c>null</c>, <paramref name="path"/> is used as the root.</param>
        /// <returns>A collection of <see cref="FileNode"/> instances for all files in the directory.</returns>
        /// <remarks>
        /// <para>
        /// Traversal logic:
        /// <list type="bullet">
        ///   <item><description>Enumerates all files in the current directory, computing the SHA-256 hash and relative path for each file.</description></item>
        ///   <item><description>If <see cref="BlackMatcher"/> is set, files matching the blacklist are skipped.</description></item>
        ///   <item><description>Recursively traverses all subdirectories, skipping those that match the blacklist.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Relative paths are computed using the <c>Uri.MakeRelativeUri</c> method to ensure cross-platform compatibility.
        /// </para>
        /// </remarks>
        private IEnumerable<FileNode> ReadFileNode(string path, string rootPath = null)
        {
            var resultFiles = new List<FileNode>();
            rootPath ??= path;
            if (!rootPath.EndsWith("/"))
            {
                rootPath += "/";
            }
            var rootUri = new Uri(rootPath);

            foreach (var subPath in Directory.EnumerateFiles(path))
            {
                if (BlackMatcher != null && BlackMatcher.IsBlacklisted(subPath)) continue;

                var hashAlgorithm = new Sha256HashAlgorithm();
                var hash = hashAlgorithm.ComputeHash(subPath);
                var subFileInfo = new FileInfo(subPath);
                var subUri = new Uri(subFileInfo.FullName);
                resultFiles.Add(new FileNode
                {
                    Id = GetId(),
                    Path = path,
                    Name = subFileInfo.Name,
                    Hash = hash,
                    FullName = subFileInfo.FullName,
                    RelativePath = rootUri.MakeRelativeUri(subUri).ToString()
                });
            }

            foreach (var subPath in Directory.EnumerateDirectories(path))
            {
                if (BlackMatcher != null && BlackMatcher.ShouldSkipDirectory(subPath)) continue;
                resultFiles.AddRange(ReadFileNode(subPath, rootPath));
            }

            return resultFiles;
        }

        /// <summary>
        /// Gets an auto-incrementing file tree node ID using a thread-safe interlocked increment operation.
        /// </summary>
        /// <returns>The next available file node ID.</returns>
        /// <remarks>
        /// This method uses <see cref="Interlocked.Increment"/> to guarantee ID uniqueness in multi-threaded environments.
        /// </remarks>
        private long GetId() => Interlocked.Increment(ref _fileCount);

        /// <summary>
        /// Resets the file tree node ID counter to 0 using a thread-safe interlocked exchange operation.
        /// </summary>
        /// <remarks>
        /// Called at the start of each new <see cref="Compare"/> operation to ensure each comparison uses an independent ID sequence.
        /// </remarks>
        private void ResetId() => Interlocked.Exchange(ref _fileCount, 0);

        /// <summary>
        /// Cleans up old backup versions, retaining only the most recent N versions.
        /// </summary>
        /// <param name="installPath">The application installation root directory path.</param>
        /// <param name="keepVersions">The number of most recent backup versions to retain. Default is 3.</param>
        /// <remarks>
        /// Scans for backup directories in two locations:
        /// 1. <c>{installPath}\{BackupRootDirectory}\</c> — new-format container for backup-{timestamp} dirs
        /// 2. <c>{installPath}\</c> directly — backup dirs matching patterns from <see cref="BlackDefaults.DefaultDirectories"/>
        /// Directories are sorted by name in descending order (both timestamp and version
        /// strings are lexicographically sortable), retaining the top N and deleting the rest.
        /// </remarks>
        public static void CleanBackup(string installPath, int keepVersions = 3)
        {
            // Scan BackupRootDirectory subdirectory (new-format backup container)
            var backupRoot = Path.Combine(installPath, BackupRootDirectory);
            if (Directory.Exists(backupRoot))
            {
                CleanDirectories(backupRoot, keepVersions);
            }

            // Scan installPath directly for backup dirs matching patterns from defaults
            foreach (var pattern in GetBackupSearchPatterns())
            {
                CleanDirectories(installPath, keepVersions, pattern);
            }
        }

        /// <summary>
        /// Derives search patterns from <see cref="BackupNamePrefixes"/>
        /// by appending "*" to each entry (e.g. "backup-" → "backup-*").
        /// </summary>
        private static IEnumerable<string> GetBackupSearchPatterns()
        {
            foreach (var prefix in BackupNamePrefixes)
            {
                yield return prefix + "*";
            }
        }

        /// <summary>
        /// Cleans directories matching the specified pattern in the given root path,
        /// retaining only the most recent N directories.
        /// </summary>
        private static void CleanDirectories(string rootPath, int keepVersions, string searchPattern = "*")
        {
            var dirs = new DirectoryInfo(rootPath)
                .GetDirectories(searchPattern, SearchOption.TopDirectoryOnly)
                .OrderByDescending(d => d.CreationTime)
                .ThenByDescending(d => d.Name)
                .Skip(keepVersions);

            foreach (var dir in dirs)
                dir.Delete(true);
        }

        /// <summary>
        /// Lists all backup versions and their metadata information.
        /// </summary>
        /// <param name="installPath">The application installation root directory path.</param>
        /// <returns>A read-only collection of <see cref="BackupInfo"/> for all backup versions.</returns>
        /// <remarks>
        /// Scans both <c>{installPath}\{BackupRootDirectory}</c> (new format) and <c>{installPath}</c> directly
        /// (backup dirs matching patterns from <see cref="BlackDefaults.DefaultDirectories"/>).
        /// Each backup entry contains the directory name, full path, creation time, and total size in bytes.
        /// </remarks>
        public static IReadOnlyList<BackupInfo> ListBackups(string installPath)
        {
            var result = new List<BackupInfo>();

            // Scan BackupRootDirectory subdirectory (new-format backup container)
            var backupRoot = Path.Combine(installPath, BackupRootDirectory);
            if (Directory.Exists(backupRoot))
            {
                result.AddRange(ToBackupInfos(backupRoot, "*"));
            }

            // Scan installPath directly for backup dirs matching patterns from defaults
            foreach (var pattern in GetBackupSearchPatterns())
            {
                result.AddRange(ToBackupInfos(installPath, pattern));
            }

            return result;
        }

        private static IEnumerable<BackupInfo> ToBackupInfos(string rootPath, string searchPattern)
        {
            return Directory.GetDirectories(rootPath, searchPattern, SearchOption.TopDirectoryOnly)
                .Select(d => new DirectoryInfo(d))
                .Select(d => new BackupInfo(
                    d.Name, d.FullName, d.CreationTime,
                    d.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)));
        }

        #endregion
    }

    /// <summary>
    /// Backup configuration for controlling backup behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuring <see cref="BackupConfig"/> controls the following:
    /// <list type="bullet">
    ///   <item><description><see cref="KeepVersions"/>: The number of historical backup versions to retain.</description></item>
    ///   <item><description><see cref="BackupRoot"/>: Custom backup root directory (optional).</description></item>
    ///   <item><description><see cref="Directories"/>: The list of subdirectory names to skip during backup.</description></item>
    ///   <item><description><see cref="Enabled"/>: Whether the backup feature is enabled.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class BackupConfig
    {
        /// <summary>
        /// The number of most recent backup versions to retain. Default is 3.
        /// </summary>
        public int KeepVersions { get; set; } = 3;

        /// <summary>
        /// Custom backup root directory path. If <c>null</c>, the default backup location is used.
        /// </summary>
        public string? BackupRoot { get; set; }

        /// <summary>
        /// The list of subdirectory names to skip during backup.
        /// </summary>
        /// <remarks>
        /// Uses containment matching (<c>string.Contains</c>) for evaluation. A directory is skipped if its name contains any string in the list.
        /// </remarks>
        public List<string> Directories { get; set; } = new();

        /// <summary>
        /// Whether the backup feature is enabled. Default is <c>false</c>.
        /// </summary>
        public bool Enabled { get; set; } = false;
    }

    /// <summary>
    /// Represents metadata for a backup version.
    /// </summary>
    /// <param name="Version">The name of the backup version (typically a version string).</param>
    /// <param name="Path">The full path to the backup directory.</param>
    /// <param name="CreatedAt">The creation time of the backup.</param>
    /// <param name="SizeBytes">The total size of the backup in bytes.</param>
    /// <remarks>
    /// This record type is used as the return type for the <see cref="StorageManager.ListBackups"/> method,
    /// providing summary information for each backup version so users can view and manage historical backups.
    /// </remarks>
    public record BackupInfo(string Version, string Path, DateTime CreatedAt, long SizeBytes);
}
