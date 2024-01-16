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
        /// ���ݲ�������ɸѡ��sourceDir��targetDir�����ļ�·���з���Ҫ����ļ���Ϣ
        /// </summary>
        /// <param name="sourceDir">ԴĿ¼</param>
        /// <param name="targetDir">Ŀ��Ŀ¼</param>
        /// <param name="resultDir">ɸѡ�������Ŀ¼</param>
        /// <param name="condition">ɸѡ�������������ļ������������ļ���׺��</param>
        /// <param name="fileOption">�ļ���ִ�еĲ���������ɾ���顢�ġ�����</param>
        /// <param name="setOperations">�������㣺�������������</param>
        /// <param name="recursion">����������Ĳ����Ƿ�ݹ�ִ��</param>
        /// <param name="integrality">�Ƿ񱣳�Ŀ¼�ṹ��������</param>
        /// <returns></returns>
        //public List<FileNode> Handle(string sourceDir,string targetDir, string resultDir, List<string> condition, FileOperations fileOption, SetOperations setOperations,bool recursion,bool integrality) 
        //{
        //    return new List<FileNode>();
        //}

        public List<FileNode> Handle(string sourceDir, string targetDir, string resultDir, List<string> condition, FileOperations fileOption, SetOperations setOperations, bool recursion, bool integrality)
        {
            // ���Ȼ�ȡԴĿ¼��Ŀ��Ŀ¼�����е��ļ���Ϣ
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

            // Ȼ���������ɸѡ����Ҫ����ļ�
            IEnumerable<FileNode> filteredSourceFiles = sourceFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));
            IEnumerable<FileNode> filteredTargetFiles = targetFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));

            // ���м��������Եõ����յĽ��
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

            // ִ���ļ�����
            foreach (FileNode file in resultFiles)
            {
                ExecuteFileOperation(file, fileOption, resultDir, integrality);
            }

            return resultFiles.ToList();
        }

        /// <summary>
        /// �����ļ��Ĺ�ϣֵ.
        /// </summary>
        /// <param name="file">�ļ�����.</param>
        /// <returns></returns>
        private string CalculateFileHash(FileInfo file)
        {
            // ����ֻ��һ��ռλ����ʵ��������Ҫʵ��һ�������������ļ���ϣֵ.
            return string.Empty;
        }

        /// <summary>
        /// ִ���ļ�����.
        /// </summary>
        /// <param name="file">�ļ�����.</param>
        /// <param name="operation">Ҫִ�еĲ���.</param>
        /// <param name="resultDir">���Ŀ¼.</param>
        /// <param name="retainStructure">�Ƿ����ṹ.</param>
        private void ExecuteFileOperation(FileNode file, FileOperations operation, string resultDir, bool retainStructure)
        {
            string destinationPath = Path.Combine(resultDir, retainStructure ? file.RelativePath : file.Name);

            switch (operation)
            {
                case FileOperations.Add:
                    // ����ļ��Ѵ��ڣ��˲��������������ļ�
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Delete:
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    break;
                case FileOperations.Update:
                    // ��������Ǽٶ����²�����ζ�Ÿ���Դ�ļ���Ŀ��λ�ã����������ļ�
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Copy:
                    // ȷ��Ŀ��Ŀ¼����
                    string directoryName = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    // �����ļ����µ�λ��
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Query:
                    // ���ڡ���ѯ�����������ǲ�ִ���κ��ļ�������ֻ�ڿ���̨�д�ӡ�������Ϣ
                    Console.WriteLine($"Found file: {file.FullName}");
                    break;
                default:
                    throw new ArgumentException("Invalid operation", nameof(operation));
            }
        }

    }
}