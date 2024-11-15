using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using GeneralUpdate.Common.HashAlgorithms;

namespace GeneralUpdate.Common.FileBasic
{
    public sealed class GeneralFileManager
    {
        private long _fileCount = 0;
        public ComparisonResult ComparisonResult { get; private set; }

        #region Public Methods
        
        /// <summary>
        /// Using the list on the left as a baseline, find the set of differences between the two file lists.
        /// </summary>
        public IEnumerable<FileNode> Except(string leftPath, string rightPath)
        {
            var leftFileNodes = ReadFileNode(leftPath);
            var rightFileNodes = ReadFileNode(rightPath);
            var rightNodeDic = rightFileNodes.ToDictionary(x => x.RelativePath);
            return leftFileNodes.Where(f => !rightNodeDic.ContainsKey(f.RelativePath)).ToList();
        }

        /// <summary>
        /// Compare two directories.
        /// </summary>
        /// <param name="leftDir"></param>
        /// <param name="rightDir"></param>
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

        public static void CreateJson<T>(string targetPath, T obj) where T : class
        {
            var folderPath = Path.GetDirectoryName(targetPath) ??
                             throw new ArgumentException("invalid path", nameof(targetPath));
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var jsonString = JsonSerializer.Serialize(obj);
            File.WriteAllText(targetPath, jsonString);
        }

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

        public static string GetTempDirectory(string name)
        {
            var path = $"generalupdate_{DateTime.Now:yyyy-MM-dd}_{name}";
            var tempDir = Path.Combine(Path.GetTempPath(), path);
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            return tempDir;
        }

        public static List<FileInfo> GetAllfiles(string path)
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
                return null;
            }
        }

        public static bool HashEquals(string leftPath, string rightPath)
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashLeft = hashAlgorithm.ComputeHash(leftPath);
            var hashRight = hashAlgorithm.ComputeHash(rightPath);
            return hashLeft.SequenceEqual(hashRight);
        }
        
        /// <summary>
        /// Backup the all program.
        /// </summary>
        /// <param name="backupPath"></param>
        /// <param name="sourcePath"></param>
        /// <param name="directoryName"></param>
        public static void Backup(string sourcePath, string backupPath, List<string> directoryNames)
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }
            Directory.CreateDirectory(backupPath);
            CopyDirectory(sourcePath, backupPath, directoryNames);
        }

        private static void CopyDirectory(string sourceDir, string targetDir, List<string> directoryNames)
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
        /// Restore the all program.
        /// </summary>
        /// <param name="backupPath"></param>
        /// <param name="sourcePath"></param>
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
        
        #endregion

        #region Private Methods

        /// <summary>
        /// Recursively read all files in the folder path.
        /// </summary>
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
                if (BlackListManager.Instance.IsBlacklisted(subPath)) continue;

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
                resultFiles.AddRange(ReadFileNode(subPath, rootPath));
            }

            return resultFiles;
        }

        /// <summary>
        /// Self-growing file tree node ID.
        /// </summary>
        private long GetId() => Interlocked.Increment(ref _fileCount);

        private void ResetId() => Interlocked.Exchange(ref _fileCount, 0);

        #endregion
    }
}