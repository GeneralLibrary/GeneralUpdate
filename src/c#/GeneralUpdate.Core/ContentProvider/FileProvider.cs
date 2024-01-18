using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Differential.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.ContentProvider
{
    public partial class FileProvider
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
    }
}