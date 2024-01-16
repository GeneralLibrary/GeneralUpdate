using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Differential.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.ContentProvider
{
    public enum FileOperations 
    {
        Query,
        Delete,
        Update,
        Add,
        Copy
    }

    public enum SetOperations 
    {
        Intersection,
        Union,
        Difference
    }

    public class FileProvider
    {
        private long _fileCount = 0;

        #region Public Methods

        /// <summary>
        /// Compare two binary trees with different children.
        /// </summary>
        /// <param name="leftPath">Left tree folder path.</param>
        /// <param name="rightPath">Right tree folder path.</param>
        /// <returns>ValueTuple(leftFileNodes,rightFileNodes, differentTreeNode)</returns>
        public async Task<ValueTuple<IEnumerable<FileNode>, IEnumerable<FileNode>, IEnumerable<FileNode>>> Compare(string leftPath, string rightPath)
        {
            return await Task.Run(() =>
            {
                ResetId();
                var leftFileNodes = Read(leftPath);
                var rightFileNodes = Read(rightPath);
                var leftTree = new FileTree(leftFileNodes);
                var rightTree = new FileTree(rightFileNodes);
                var differentTreeNode = new List<FileNode>();
                leftTree.Compare(leftTree.GetRoot(), rightTree.GetRoot(), ref differentTreeNode);
                return ValueTuple.Create(leftFileNodes, rightFileNodes, differentTreeNode);
            });
        }

        /// <summary>
        /// Using the list on the left as a baseline, find the set of differences between the two file lists.
        /// </summary>
        /// <param name="leftPath">Previous version file list path</param>
        /// <param name="rightPath">The current version file list path</param>
        /// <returns>Except collection</returns>
        public async Task<IEnumerable<FileNode>> Except(string leftPath, string rightPath)
        {
            return await Task.Run(() => 
            {
                var leftFileNodes = Read(leftPath);
                var rightFileNodes = Read(rightPath);
                var rightNodeDic = rightFileNodes.ToDictionary(x => x.RelativePath, x => x);
                var filesOnlyInLeft = leftFileNodes.Where(f => !rightNodeDic.ContainsKey(f.RelativePath)).ToList();
                return filesOnlyInLeft;
            });
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Recursively read all files in the folder path.
        /// </summary>
        /// <param name="path">folder path.</param>
        /// <param name="rootPath">folder root path.</param>
        /// <returns>different chalders.</returns>
        private IEnumerable<FileNode> Read(string path, string rootPath = null)
        {
            var resultFiles = new List<FileNode>();
            if (string.IsNullOrEmpty(rootPath)) rootPath = path;
            if (!rootPath.EndsWith("/")) rootPath += "/";
            Uri rootUri = new Uri(rootPath);
            foreach (var subPath in Directory.GetFiles(path))
            {
                if (IsMatchBlacklist(subPath)) continue;

                var hashAlgorithm = new Sha256HashAlgorithm();
                var hash = hashAlgorithm.ComputeHash(subPath);
                var subFileInfo = new FileInfo(subPath);
                Uri subUri = new Uri(subFileInfo.FullName);
                resultFiles.Add(new FileNode() { Id = GetId(), Path = path, Name = subFileInfo.Name, Hash = hash, FullName = subFileInfo.FullName, RelativePath = rootUri.MakeRelativeUri(subUri).ToString() });
            }
            foreach (var subPath in Directory.GetDirectories(path))
            {
                resultFiles.AddRange(Read(subPath, rootPath));
            }
            return resultFiles;
        }

        /// <summary>
        /// Self-growing file tree node ID.
        /// </summary>
        /// <returns></returns>
        private long GetId() => Interlocked.Increment(ref _fileCount);

        private void ResetId() => Interlocked.Exchange(ref _fileCount, 0);

        /// <summary>
        /// Whether the file name in the file path can match the blacklisted file.
        /// </summary>
        /// <param name="subPath"></param>
        /// <returns></returns>
        private bool IsMatchBlacklist(string subPath)
        {
            var blackList = Filefilter.GetBlackFiles();
            if (blackList == null) return false;
            foreach (var file in blackList)
            {
                if (subPath.Contains(file)) return true;
            }
            return false;
        }

        #endregion Private Methods

        /// <summary>
        /// 根据参数内容筛选出sourceDir、targetDir两个文件路径中符合要求的文件信息
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        /// <param name="resultDir">筛选结果保存目录</param>
        /// <param name="condition">筛选条件，可以是文件名，可以是文件后缀名</param>
        /// <param name="fileOption">文件可执行的操作：增、删、查、改、拷贝</param>
        /// <param name="setOperations">集合运算：交集、并集、差集</param>
        /// <param name="recursion">整个方法里的操作是否递归执行</param>
        /// <param name="integrality">是否保持目录结构的完整性</param>
        /// <returns></returns>
        //public List<FileNode> Handle(string sourceDir,string targetDir, string resultDir, List<string> condition, FileOperations fileOption, SetOperations setOperations,bool recursion,bool integrality) 
        //{
        //    return new List<FileNode>();
        //}

        public List<FileNode> Handle(string sourceDir, string targetDir, string resultDir, List<string> condition, FileOperations fileOption, SetOperations setOperations, bool recursion, bool integrality)
        {
            // 首先获取源目录和目标目录中所有的文件信息
            IEnumerable<FileNode> sourceFiles = Directory.EnumerateFiles(sourceDir, "*.*", recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Select(fileInfo => new FileNode
                {
                    Id = fileInfo.GetHashCode(),
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    Path = fileInfo.DirectoryName,
                    Hash = CalculateFileHash(fileInfo)
                });
            IEnumerable<FileNode> targetFiles = Directory.EnumerateFiles(targetDir, "*.*", recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Select(fileInfo => new FileNode
                {
                    Id = fileInfo.GetHashCode(),
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    Path = fileInfo.DirectoryName,
                    Hash = CalculateFileHash(fileInfo)
                });

            // 然后根据条件筛选满足要求的文件
            IEnumerable<FileNode> filteredSourceFiles = sourceFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));
            IEnumerable<FileNode> filteredTargetFiles = targetFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));

            // 进行集合运算以得到最终的结果
            IEnumerable<FileNode> resultFiles;
            switch (setOperations)
            {
                case SetOperations.Intersection:
                    resultFiles = filteredSourceFiles.Intersect(filteredTargetFiles);
                    break;
                case SetOperations.Union:
                    resultFiles = filteredSourceFiles.Union(filteredTargetFiles);
                    break;
                case SetOperations.Difference:
                    resultFiles = filteredSourceFiles.Except(filteredTargetFiles);
                    break;
                default:
                    throw new ArgumentException("Invalid operation.", nameof(setOperations));
            }

            // 执行文件操作
            foreach (FileNode file in resultFiles)
            {
                ExecuteFileOperation(file, fileOption, resultDir, integrality);
            }

            return resultFiles.ToList();
        }

        /// <summary>
        /// 计算文件的哈希值.
        /// </summary>
        /// <param name="file">文件对象.</param>
        /// <returns></returns>
        private string CalculateFileHash(FileInfo file)
        {
            // 这里只是一个占位符，实际上你需要实现一个方法来计算文件哈希值.
            return string.Empty;
        }

        /// <summary>
        /// 执行文件操作.
        /// </summary>
        /// <param name="file">文件对象.</param>
        /// <param name="operation">要执行的操作.</param>
        /// <param name="resultDir">结果目录.</param>
        /// <param name="retainStructure">是否保留结构.</param>
        private void ExecuteFileOperation(FileNode file, FileOperations operation, string resultDir, bool retainStructure)
        {
            string destinationPath = Path.Combine(resultDir, retainStructure ? file.RelativePath : file.Name);

            switch (operation)
            {
                case FileOperations.Add:
                    // 如果文件已存在，此操作将覆盖现有文件
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Delete:
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    break;
                case FileOperations.Update:
                    // 在这里，我们假定更新操作意味着复制源文件到目标位置，覆盖现有文件
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Copy:
                    // 确保目标目录存在
                    string directoryName = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    // 拷贝文件到新的位置
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Query:
                    // 对于“查询”操作，我们不执行任何文件操作，只在控制台中打印出相关信息
                    Console.WriteLine($"Found file: {file.FullName}");
                    break;
                default:
                    throw new ArgumentException("Invalid operation", nameof(operation));
            }
        }

    }
}