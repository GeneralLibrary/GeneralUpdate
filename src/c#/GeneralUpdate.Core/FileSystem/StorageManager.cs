using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using GeneralUpdate.Core.HashAlgorithms;

namespace GeneralUpdate.Core.FileSystem
{
    /// <summary>
    /// 存储管理器，提供文件系统操作的静态工具类。
    /// 支持备份、恢复、目录比较、文件遍历、哈希校验以及黑名单过滤等核心功能。
    /// 该类是所有文件系统操作的统一入口，在更新流程中负责版本目录的快照生成与差异比较。
    /// </summary>
    /// <remarks>
    /// <para>
    /// StorageManager 是整个更新框架中文件操作的核心枢纽，主要职责包括：
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>Backup</c> / <c>Restore</c>：创建和恢复应用程序的完整备份。</description></item>
    ///   <item><description><c>Compare</c>：对两个目录进行递归比较，识别新增、修改和删除的文件。</description></item>
    ///   <item><description><c>HashEquals</c>：使用 SHA-256 哈希算法验证两个文件是否相同。</description></item>
    ///   <item><description><c>GetAllFiles</c>：递归获取指定目录下的所有文件，支持跳过指定目录。</description></item>
    ///   <item><description><c>CleanBackup</c> / <c>ListBackups</c>：管理历史备份版本，支持保留最近 N 个版本。</description></item>
    /// </list>
    /// <para>
    /// 可通过 <see cref="BlackListMatcher"/> 静态属性设置黑名单匹配器，在文件遍历时排除特定文件或目录。
    /// 所有公开方法均为线程安全的静态方法，但 <see cref="Compare"/> 方法内部使用实例状态，注意并发调用。
    /// </para>
    /// </remarks>
    public sealed class StorageManager
    {
        private long _fileCount = 0;

        /// <summary>
        /// 备份目录的默认前缀名称。
        /// </summary>
        /// <remarks>
        /// 备份目录的命名格式为 "app-{版本号}"，此常量定义了前缀部分。
        /// </remarks>
        public const string DirectoryName = "app-";

        /// <summary>
        /// 获取或设置可选的路径/文件黑名单匹配器。
        /// </summary>
        /// <value>
        /// 实现 <see cref="IBlackListMatcher"/> 接口的实例，用于在文件遍历时排除黑名单中的文件或目录。
        /// 必须在执行任何文件操作之前设置。
        /// </value>
        /// <remarks>
        /// 如果设置了此属性，<see cref="ReadFileNode"/> 方法会在遍历文件系统时自动跳过匹配的文件和目录。
        /// 设置方式示例：<c>StorageManager.BlackListMatcher = new DefaultBlackListMatcher(config);</c>
        /// </remarks>
        public static IBlackListMatcher? BlackListMatcher { get; set; }
        
        private ComparisonResult ComparisonResult { get; set; }

        #region Public Methods

        /// <summary>
        /// 以左侧目录为基准，找出左侧有但右侧没有的文件集合（即被删除的文件）。
        /// </summary>
        /// <param name="leftPath">基准（旧版本）目录路径。</param>
        /// <param name="rightPath">目标（新版本）目录路径。</param>
        /// <returns>
        /// 存在于左侧但不存在于右侧的 <see cref="FileNode"/> 集合；如果两侧文件列表完全一致，则返回空集合。
        /// </returns>
        /// <remarks>
        /// 此方法将左右两侧目录分别序列化为 <see cref="FileNode"/> 列表，
        /// 然后以 <c>RelativePath</c> 为键构建哈希表进行差集运算。
        /// 适用于差异更新场景中识别需要删除的旧文件。
        /// </remarks>
        public IEnumerable<FileNode>? Except(string leftPath, string rightPath)
        {
            var leftFileNodes = ReadFileNode(leftPath);
            var rightFileNodes = ReadFileNode(rightPath);
            var rightNodeDic = rightFileNodes.ToDictionary(x => x.RelativePath);
            return leftFileNodes.Where(f => !rightNodeDic.ContainsKey(f.RelativePath)).ToList();
        }

        /// <summary>
        /// 比较两个目录，识别出其中不同的文件。
        /// </summary>
        /// <param name="leftDir">基准（旧版本）目录路径。</param>
        /// <param name="rightDir">目标（新版本）目录路径。</param>
        /// <returns>
        /// <see cref="ComparisonResult"/> 对象，包含左侧节点、右侧节点以及差异节点的集合。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 比较流程如下：
        /// <list type="number">
        ///   <item><description>重置内部文件 ID 计数器。</description></item>
        ///   <item><description>递归读取左右两个目录中的所有文件节点，生成 <see cref="FileNode"/> 列表。</description></item>
        ///   <item><description>分别构建左右两棵 <see cref="FileTree"/> 二叉排序树。</description></item>
        ///   <item><description>从根节点开始递归对比两棵树的同名节点，收集哈希值或名称不同的节点。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 注意：该方法使用实例内部的 <c>ComparisonResult</c> 状态，应避免在多线程环境中并发调用同一实例。
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
        /// 将对象序列化为 JSON 文件并写入指定路径。
        /// </summary>
        /// <typeparam name="T">要序列化的对象类型，必须是引用类型。</typeparam>
        /// <param name="targetPath">目标 JSON 文件的完整路径。</param>
        /// <param name="obj">要序列化的对象实例。</param>
        /// <param name="typeInfo">可选的 JSON 类型信息元数据，用于支持源生成器序列化。</param>
        /// <exception cref="ArgumentException">当 <paramref name="targetPath"/> 不包含有效的目录路径时抛出。</exception>
        /// <remarks>
        /// 如果目标文件的目录不存在，会自动创建。支持通过 <c>JsonTypeInfo</c> 进行源生成器模式，
        /// 在 AOT 编译场景中可避免运行时反射。
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
        /// 从指定路径读取 JSON 文件并反序列化为指定类型的对象。
        /// </summary>
        /// <typeparam name="T">要反序列化的目标类型，必须是引用类型。</typeparam>
        /// <param name="path">JSON 文件的完整路径。</param>
        /// <param name="typeInfo">可选的 JSON 类型信息元数据，用于支持源生成器反序列化。</param>
        /// <returns>反序列化后的对象实例；如果文件不存在则返回 <c>default</c>。</returns>
        /// <remarks>
        /// 如果文件不存在，不会抛出异常而是返回 <c>null</c>。
        /// 支持通过 <c>JsonTypeInfo</c> 进行源生成器模式。
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
        /// 在系统临时目录中创建一个带有唯一名称的子目录，用于存放临时更新文件。
        /// </summary>
        /// <param name="name">用于标识临时目录用途的自定义名称。</param>
        /// <returns>创建的临时目录的完整路径。</returns>
        /// <remarks>
        /// 目录命名格式为 <c>generalupdate_{时间戳}_{进程ID}_{name}</c>。
        /// 如果目录已存在不会重复创建。调用方负责在不再需要时清理此目录。
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
        /// 递归删除指定目录及其所有子目录和文件。
        /// </summary>
        /// <param name="targetDir">要删除的目标目录路径。</param>
        /// <remarks>
        /// 在删除前会将每个文件的属性重置为 <see cref="FileAttributes.Normal"/>，
        /// 以避免因只读属性导致删除失败。此操作不可恢复，请谨慎使用。
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
        /// 递归获取指定目录下的所有文件，支持跳过黑名单中的子目录。
        /// </summary>
        /// <param name="path">要遍历的根目录路径。</param>
        /// <param name="skipDirectorys">需要跳过的子目录名称列表（包含匹配）。</param>
        /// <returns>所有未跳过的文件的 <see cref="FileInfo"/> 集合。</returns>
        /// <remarks>
        /// 此方法仅跳过第一层子目录（不递归跳过），适用于备份和全量文件枚举场景。
        /// 如果遍历过程中因权限等原因发生异常，会返回空集合而不是抛出异常。
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
            catch
            {
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// 私有递归方法，获取指定路径下的所有文件（无黑名单过滤）。
        /// </summary>
        /// <param name="path">要遍历的目录路径。</param>
        /// <returns>目录中所有文件的 <see cref="FileInfo"/> 集合。</returns>
        /// <remarks>
        /// 与 <see cref="GetAllFiles"/> 不同，此方法不包含目录跳过逻辑。
        /// 如果遍历过程中因权限等原因发生异常，会返回空集合而不是抛出异常。
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
            catch (Exception)
            {
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// 使用 SHA-256 哈希算法比较两个文件的内容是否完全相同。
        /// </summary>
        /// <param name="leftPath">第一个文件的完整路径。</param>
        /// <param name="rightPath">第二个文件的完整路径。</param>
        /// <returns>如果两个文件的哈希值相同则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 此方法计算两个文件的 SHA-256 哈希值并进行字节序列比较。
        /// 适用于大文件比较场景，比逐字节读取更高效。
        /// </remarks>
        public static bool HashEquals(string leftPath, string rightPath)
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashLeft = hashAlgorithm.ComputeHash(leftPath);
            var hashRight = hashAlgorithm.ComputeHash(rightPath);
            return hashLeft.SequenceEqual(hashRight);
        }

        /// <summary>
        /// 备份整个应用程序目录到指定位置。
        /// </summary>
        /// <param name="sourcePath">源应用程序目录路径。</param>
        /// <param name="backupPath">目标备份目录路径。</param>
        /// <param name="directoryNames">需要跳过的子目录名称列表（包含匹配）。</param>
        /// <remarks>
        /// <para>
        /// 备份流程：
        /// <list type="number">
        ///   <item><description>如果备份目录已存在，先删除它。</description></item>
        ///   <item><description>创建新的备份目录。</description></item>
        ///   <item><description>递归复制源目录中的所有文件和子目录，跳过 <paramref name="directoryNames"/> 中匹配的目录。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 此方法会覆盖目标目录中已存在的文件。
        /// </para>
        /// </remarks>
        public static void Backup(string sourcePath, string backupPath, IReadOnlyList<string> directoryNames)
        {
            if (Directory.Exists(backupPath))
            {
                DeleteDirectory(backupPath);
            }
            Directory.CreateDirectory(backupPath);
            CopyDirectory(sourcePath, backupPath, directoryNames);
        }

        private static void CopyDirectory(string sourceDir, string targetDir, IReadOnlyList<string> directoryNames)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                if (!directoryNames.Any(name => Path.GetFileName(dirPath).Contains(name)))
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
        /// 从备份目录恢复整个应用程序到指定位置。
        /// </summary>
        /// <param name="backupPath">备份目录路径。</param>
        /// <param name="sourcePath">要恢复到的目标应用程序目录路径。</param>
        /// <remarks>
        /// 如果目标目录不存在，会自动创建。恢复操作会完整复制备份目录中所有文件和子目录到目标位置，
        /// 并覆盖已存在的同名文件。此方法不包含黑名单过滤逻辑，会完整恢复所有备份内容。
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
        /// 异步备份整个应用程序目录（将同步备份操作调度到线程池执行）。
        /// </summary>
        /// <param name="sourcePath">源应用程序目录路径。</param>
        /// <param name="backupPath">目标备份目录路径。</param>
        /// <param name="directoryNames">需要跳过的子目录名称列表。</param>
        /// <returns>表示异步备份操作的任务。</returns>
        /// <remarks>
        /// 此方法通过 <c>Task.Run</c> 将 <see cref="Backup"/> 调用调度到线程池，
        /// 适用于 UI 应用程序中避免阻塞主线程的场景。
        /// </remarks>
        public static async System.Threading.Tasks.Task BackupAsync(string sourcePath, string backupPath, System.Collections.Generic.IReadOnlyList<string> directoryNames)
        {
            await System.Threading.Tasks.Task.Run(() => Backup(sourcePath, backupPath, directoryNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步从备份目录恢复应用程序（将同步恢复操作调度到线程池执行）。
        /// </summary>
        /// <param name="backupPath">备份目录路径。</param>
        /// <param name="sourcePath">要恢复到的目标应用程序目录路径。</param>
        /// <returns>表示异步恢复操作的任务。</returns>
        /// <remarks>
        /// 此方法通过 <c>Task.Run</c> 将 <see cref="Restore"/> 调用调度到线程池。
        /// </remarks>
        public static async System.Threading.Tasks.Task RestoreAsync(string backupPath, string sourcePath)
        {
            await System.Threading.Tasks.Task.Run(() => Restore(backupPath, sourcePath)).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步清理旧版本备份，仅保留最近的 N 个版本（将同步清理操作调度到线程池执行）。
        /// </summary>
        /// <param name="installPath">应用程序安装根目录路径。</param>
        /// <param name="keepVersions">要保留的最新备份版本数量，默认为 3。</param>
        /// <returns>表示异步清理操作的任务。</returns>
        /// <remarks>
        /// 此方法通过 <c>Task.Run</c> 将 <see cref="CleanBackup"/> 调用调度到线程池。
        /// </remarks>
        public static async System.Threading.Tasks.Task CleanBackupAsync(string installPath, int keepVersions = 3)
        {
            await System.Threading.Tasks.Task.Run(() => CleanBackup(installPath, keepVersions)).ConfigureAwait(false);
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// 私有递归方法，读取指定目录下的所有文件并转换为 <see cref="FileNode"/> 列表。
        /// </summary>
        /// <param name="path">当前要遍历的目录路径。</param>
        /// <param name="rootPath">根目录路径，用于计算相对路径。如果为 <c>null</c>，则使用 <paramref name="path"/> 作为根目录。</param>
        /// <returns>目录中所有 <see cref="FileNode"/> 的集合。</returns>
        /// <remarks>
        /// <para>
        /// 遍历逻辑：
        /// <list type="bullet">
        ///   <item><description>枚举当前目录中的所有文件，计算每个文件的 SHA-256 哈希值和相对路径。</description></item>
        ///   <item><description>如果设置了 <see cref="BlackListMatcher"/>，会跳过匹配黑名单的文件。</description></item>
        ///   <item><description>递归遍历所有子目录，跳过匹配黑名单的子目录。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 相对路径的计算使用 <c>Uri.MakeRelativeUri</c> 方法，确保跨平台兼容性。
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
                if (BlackListMatcher != null && BlackListMatcher.IsBlacklisted(subPath)) continue;

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
                if (BlackListMatcher != null && BlackListMatcher.ShouldSkipDirectory(subPath)) continue;
                resultFiles.AddRange(ReadFileNode(subPath, rootPath));
            }

            return resultFiles;
        }

        /// <summary>
        /// 获取自增的文件树节点 ID，使用线程安全的交错增量操作。
        /// </summary>
        /// <returns>下一个可用的文件节点 ID。</returns>
        /// <remarks>
        /// 此方法通过 <see cref="Interlocked.Increment"/> 保证多线程环境下的 ID 唯一性。
        /// </remarks>
        private long GetId() => Interlocked.Increment(ref _fileCount);

        /// <summary>
        /// 重置文件树节点 ID 计数器为 0，使用线程安全的交错交换操作。
        /// </summary>
        /// <remarks>
        /// 在每次新的 <see cref="Compare"/> 操作开始时调用，以确保每个比较操作使用独立的 ID 序列。
        /// </remarks>
        private void ResetId() => Interlocked.Exchange(ref _fileCount, 0);

        /// <summary>
        /// 清理旧的备份版本，仅保留最近的 N 个版本。
        /// </summary>
        /// <param name="installPath">应用程序安装根目录路径。</param>
        /// <param name="keepVersions">要保留的最新备份版本数量，默认为 3。</param>
        /// <remarks>
        /// 备份目录位于 <c>{installPath}/__backups</c> 下，每个子目录以版本号命名。
        /// 此方法按照版本号降序排列，保留前 N 个版本，删除其余所有版本。
        /// 如果版本号解析失败，视为 <c>0.0</c> 版本（会优先被删除）。
        /// 如果 <c>__backups</c> 目录不存在，则不执行任何操作。
        /// </remarks>
        public static void CleanBackup(string installPath, int keepVersions = 3)
        {
            var backupRoot = Path.Combine(installPath, "__backups");
            if (!Directory.Exists(backupRoot)) return;

            var dirs = Directory.GetDirectories(backupRoot)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d =>
                {
                    var name = d.Name;
                    return Version.TryParse(name, out var v) ? v : new Version(0, 0);
                })
                .Skip(keepVersions);

            foreach (var dir in dirs)
                dir.Delete(true);
        }

        /// <summary>
        /// 列出所有备份版本及其元数据信息。
        /// </summary>
        /// <param name="installPath">应用程序安装根目录路径。</param>
        /// <returns>所有备份版本的 <see cref="BackupInfo"/> 只读集合。</returns>
        /// <remarks>
        /// 每个备份条目包含版本号、完整路径、创建时间和总大小（字节）。
        /// 如果 <c>__backups</c> 目录不存在，返回空集合。
        /// </remarks>
        public static IReadOnlyList<BackupInfo> ListBackups(string installPath)
        {
            var backupRoot = Path.Combine(installPath, "__backups");
            if (!Directory.Exists(backupRoot)) return Array.Empty<BackupInfo>();

            return Directory.GetDirectories(backupRoot)
                .Select(d => new DirectoryInfo(d))
                .Select(d => new BackupInfo(
                    d.Name, d.FullName, d.CreationTime,
                    d.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)))
                .ToList();
        }

        #endregion
    }

    /// <summary>
    /// 备份配置项，用于控制备份行为。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 通过配置 <see cref="BackupConfig"/> 可以控制：
    /// <list type="bullet">
    ///   <item><description><see cref="KeepVersions"/>：保留的历史备份版本数量。</description></item>
    ///   <item><description><see cref="BackupRoot"/>：自定义备份根目录（可选）。</description></item>
    ///   <item><description><see cref="SkipDirectories"/>：备份时需要跳过的子目录列表。</description></item>
    ///   <item><description><see cref="Enabled"/>：是否启用备份功能。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class BackupConfig
    {
        /// <summary>
        /// 要保留的最新备份版本数量，默认为 3。
        /// </summary>
        public int KeepVersions { get; set; } = 3;

        /// <summary>
        /// 自定义备份根目录路径。如果为 <c>null</c>，则使用默认备份位置。
        /// </summary>
        public string? BackupRoot { get; set; }

        /// <summary>
        /// 备份时需要跳过的子目录名称列表。
        /// </summary>
        /// <remarks>
        /// 使用包含匹配（<c>string.Contains</c>）进行判断，只要目录名包含列表中的任一字符串即被跳过。
        /// </remarks>
        public List<string> SkipDirectories { get; set; } = new();

        /// <summary>
        /// 是否启用备份功能，默认为 <c>true</c>。
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 备份版本元数据记录。
    /// </summary>
    /// <param name="Version">备份版本的名称（一般为版本号字符串）。</param>
    /// <param name="Path">备份目录的完整路径。</param>
    /// <param name="CreatedAt">备份的创建时间。</param>
    /// <param name="SizeBytes">备份的总大小（字节数）。</param>
    /// <remarks>
    /// 此记录类型用于 <see cref="StorageManager.ListBackups"/> 方法的返回结果，
    /// 提供每个备份版本的摘要信息以便用户查看和管理历史备份。
    /// </remarks>
    public record BackupInfo(string Version, string Path, DateTime CreatedAt, long SizeBytes);
}
