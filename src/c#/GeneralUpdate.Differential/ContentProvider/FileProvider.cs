using GeneralUpdate.Core.Utils;
using GeneralUpdate.Differential.ContentProvider.FileTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.ContentProvider
{
    public class FileProvider
    {
        private long _fileCount = 0;

        public void Compare(string leftPath, string rightPath)
        {
            var leftFilenodes = ReadAsync(leftPath);
            var rightFilenodes = ReadAsync(rightPath);
            //FileTree left = new FileTree();
        }

        private IEnumerable<FileNode> ReadAsync(string path)
        {
            var resultFiles = new List<FileNode>();
            Parallel.ForEach(Directory.GetFiles(path), (subPath) => 
            {
                string md5 =  FileUtil.GetFileMD5(subPath);
                FileInfo subFileInfo = new FileInfo(subPath);
                resultFiles.Add(new FileNode() { Id = GetId() , Path = path , Name = subFileInfo.Name , MD5 = md5 });
            });
            Parallel.ForEach(Directory.GetDirectories(path), (subPath) =>
            {
                resultFiles.AddRange(ReadAsync(subPath));
            });
            ResetId();
            return resultFiles;
        }

        private long GetId()=> Interlocked.Increment(ref _fileCount);

        private void ResetId()=> Interlocked.Exchange(ref _fileCount, 0);
    }
}
