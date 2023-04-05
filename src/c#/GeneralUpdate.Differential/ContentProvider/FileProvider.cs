using GeneralUpdate.Core.Utils;
using GeneralUpdate.Differential.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.ContentProvider
{
    public class FileProvider
    {
        #region Private Members

        private long _fileCount = 0;

        #endregion Private Members

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
                var resultNodes = new List<FileNode>();
                foreach (var leftNode in leftFileNodes)
                {
                    var findObj = rightFileNodes.FirstOrDefault(i => i.Equals(leftNode));
                    if (findObj == null) resultNodes.Add(leftNode);
                }
                return resultNodes;
            });
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Recursively read all files in the folder path.
        /// </summary>
        /// <param name="path">folder path.</param>
        /// <returns>different chalders.</returns>
        private IEnumerable<FileNode> Read(string path)
        {
            var resultFiles = new List<FileNode>();
            foreach (var subPath in Directory.GetFiles(path))
            {
                if (IsMatchBlacklist(subPath)) continue;
                var md5 = FileUtil.GetFileMD5(subPath);
                var subFileInfo = new FileInfo(subPath);
                resultFiles.Add(new FileNode() { Id = GetId(), Path = path, Name = subFileInfo.Name, MD5 = md5, FullName = subFileInfo.FullName });
            }
            foreach (var subPath in Directory.GetDirectories(path))
            {
                resultFiles.AddRange(Read(subPath));
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