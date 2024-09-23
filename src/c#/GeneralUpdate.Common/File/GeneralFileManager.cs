using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GeneralUpdate.Common.HashAlgorithms;

namespace GeneralUpdate.Common
{
    public sealed class GeneralFileManager
    {
        #region Public Properties
        
        public ComparisonResult ComparisonResult { get; private set; }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Compare two directories.
        /// </summary>
        /// <param name="dirA"></param>
        /// <param name="dirB"></param>
        public void CompareDirectories(string dirA, string dirB)
        {
            ComparisonResult = new ComparisonResult();

            var filesA = GetRelativeFilePaths(dirA, dirA).Where(f => !BlackListManager.Instance.IsBlacklisted(f)).ToList();
            var filesB = GetRelativeFilePaths(dirB, dirB).Where(f => !BlackListManager.Instance.IsBlacklisted(f)).ToList();

            ComparisonResult.AddUniqueToA(filesA.Except(filesB).Select(f => Path.Combine(dirA, f)));
            ComparisonResult.AddUniqueToB(filesB.Except(filesA).Select(f => Path.Combine(dirB, f)));

            var commonFiles = filesA.Intersect(filesB);

            foreach (var file in commonFiles)
            {
                var fileA = Path.Combine(dirA, file);
                var fileB = Path.Combine(dirB, file);

                if (!FilesAreEqual(fileA, fileB))
                {
                    ComparisonResult.AddDifferentFiles(new[] { file });
                }
            }
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

        public static T? GetJson<T>(string path) where T : class
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
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
        
        #endregion

        #region Private Methods
        
        private IEnumerable<string> GetRelativeFilePaths(string rootDir, string currentDir)
        {
            foreach (var file in Directory.GetFiles(currentDir))
            {
                yield return GetRelativePath(rootDir, file);
            }

            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                foreach (var file in GetRelativeFilePaths(rootDir, dir))
                {
                    yield return file;
                }
            }
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            } // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private bool FilesAreEqual(string fileA, string fileB)
        {
            var sha256 = new Sha256HashAlgorithm();
            var hashA = sha256.ComputeHash(fileA);
            var hashB = sha256.ComputeHash(fileB);

            return hashA.SequenceEqual(hashB);
        }
        
        #endregion
    }
}