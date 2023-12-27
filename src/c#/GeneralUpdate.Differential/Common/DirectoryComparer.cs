using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Differential.Common
{
    internal class DirectoryComparer
    {
        private readonly string _directoryA;
        private readonly string _directoryB;

        public DirectoryComparer(string directoryA, string directoryB)
        {
            this._directoryA = directoryA.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            this._directoryB = directoryB.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public List<FileInfo> Comparer()
        {
            var filesInDirectoryA = new HashSet<string>(GetAllFiles(_directoryA).Select(file => file.Substring(_directoryA.Length)), StringComparer.InvariantCultureIgnoreCase);
            var missingFilesPath = GetAllFiles(_directoryB).Where(fileB => !filesInDirectoryA.Contains(fileB.Substring(_directoryB.Length))).ToList();
            var missingFiles = missingFilesPath.Select(path => new FileInfo(path)).ToList();
            return missingFiles;
        }

        private IEnumerable<string> GetAllFiles(string directoryPath)
        {
            var directories = new Stack<string>();
            directories.Push(directoryPath);

            while (directories.Count > 0)
            {
                var currentDirectory = directories.Pop();

                if (Directory.EnumerateFiles(currentDirectory, "*.inf").Any())
                    continue;

                IEnumerable<string> currentFiles;
                try
                {
                    currentFiles = Directory.EnumerateFiles(currentDirectory);
                }
                catch
                {
                    continue;
                }

                foreach (var file in currentFiles)
                {
                    yield return file;
                }

                IEnumerable<string> subDirectories;
                try
                {
                    subDirectories = Directory.EnumerateDirectories(currentDirectory);
                }
                catch
                {
                    continue;
                }

                foreach (var subDirectory in subDirectories)
                {
                    directories.Push(subDirectory);
                }
            }
        }
    }
}
