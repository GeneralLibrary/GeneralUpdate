using GeneralUpdate.Core.Exceptions;
using GeneralUpdate.Core.HashAlgorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.ContentProvider
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

    public partial class FileProvider
    {
        public List<FileNode> ExecuteOperation(string sourceDir, string targetDir, List<string> extensionsCondition,  List<string> filenamesCondition) 
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir) || extensionsCondition == null || filenamesCondition == null)
                ThrowExceptionUtility.ThrowIfNull();

            var filesInDirA  = GetFilesWithSHA256(sourceDir, extensionsCondition, filenamesCondition);
            var filesInDirB =  GetFilesWithSHA256(targetDir, extensionsCondition, filenamesCondition);

            var inBNotInA = InFirstNotInSecond(filesInDirA, filesInDirB);
            var inANotInB = InFirstNotInSecond(filesInDirA, filesInDirB);
            return new List<FileNode>();
        }

        bool ShouldSkipFile(string filePath, IList<string> extensionsToSkip, IList<string> filenamesToSkip)
        {
            var fileInfo = new FileInfo(filePath);
            return extensionsToSkip.Contains(fileInfo.Extension) || filenamesToSkip.Contains(fileInfo.Name);
        }

        Dictionary<string, string> GetFilesWithSHA256(string path, IList<string> extensionsToSkip, IList<string> filenamesToSkip)
        {
            var result = new Dictionary<string, string>();
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!ShouldSkipFile(file, extensionsToSkip, filenamesToSkip))
                {
                    var hashAlgorithm = new Sha256HashAlgorithm();
                    result[file] = hashAlgorithm.ComputeHash(file);
                }
            }
            return result;
        }

        IEnumerable<string> InFirstNotInSecond(Dictionary<string, string> first, Dictionary<string, string> second)
        {
            foreach (var pair in first)
            {
                string value;
                if (!second.TryGetValue(pair.Key, out value) || !value.Equals(pair.Value))
                    yield return pair.Key;
            }
        }

        public static string GetTempDirectory(string name)
        {
            var path2 = $"generalupdate_{DateTime.Now.ToString("yyyy-MM-dd")}_{name}";
            var tempDir = Path.Combine(Path.GetTempPath(), path2);
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            return tempDir;
        }

        public static FileInfo[] GetAllFiles(string path)
        {
            try
            {
                var files = new List<FileInfo>();
                files.AddRange(new DirectoryInfo(path).GetFiles());
                var tmpDir = new DirectoryInfo(path).GetDirectories();
                foreach (var dic in tmpDir)
                {
                    files.AddRange(GetAllFiles(dic.FullName));
                }
                return files.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Delete the backup file directory and recursively delete all backup content.
        /// </summary>
        public static void DeleteDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}