using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeneralUpdate.Common.HashAlgorithms;
using Newtonsoft.Json;

namespace GeneralUpdate.Common
{
    public sealed class GeneralFileManager
    {
        #region Private Members
        
        private static readonly List<string> _blackFileFormats = new List<string>
        {
            ".patch",
            ".7z",
            ".zip",
            ".rar",
            ".tar",
            ".json"
        };

        private static readonly List<string> _blackFiles = new List<string> { "Newtonsoft.Json.dll" };
        
        #endregion

        #region Public Properties
        
        public static IReadOnlyList<string> BlackFileFormats => _blackFileFormats.AsReadOnly();
        public static IReadOnlyList<string> BlackFiles => _blackFiles.AsReadOnly();
        
        public ComparisonResult ComparisonResult { get; private set; }
        
        #endregion

        #region Public Methods
        
        public static void AddBlackFileFormat(string format)
        {
            if (!_blackFileFormats.Contains(format))
            {
                _blackFileFormats.Add(format);
            }
        }

        public static void RemoveBlackFileFormat(string format)
        {
            _blackFileFormats.Remove(format);
        }

        public static void AddBlackFile(string fileName)
        {
            if (!_blackFiles.Contains(fileName))
            {
                _blackFiles.Add(fileName);
            }
        }

        public static void RemoveBlackFile(string fileName)
        {
            _blackFiles.Remove(fileName);
        }
        
        /// <summary>
        /// Compare two directories.
        /// </summary>
        /// <param name="dirA"></param>
        /// <param name="dirB"></param>
        public void CompareDirectories(string dirA, string dirB)
        {
            ComparisonResult = new ComparisonResult();

            var filesA = GetRelativeFilePaths(dirA, dirA).Where(f => !IsBlacklisted(f)).ToList();
            var filesB = GetRelativeFilePaths(dirB, dirB).Where(f => !IsBlacklisted(f)).ToList();

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
        
        public static void CreateJson<T>(string targetPath, T obj)
        {
            var folderPath = Path.GetDirectoryName(targetPath) ??
                             throw new ArgumentException("invalid path", nameof(targetPath));
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            var jsonString = JsonConvert.SerializeObject(obj);
            File.WriteAllText(targetPath, jsonString);
        }

        public static T? GetJson<T>(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
            return default(T);
        }

        /// <summary>
        /// Convert object to base64 string.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object? obj)
        {
            if (obj == null) return string.Empty;
            var json = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.Default.GetBytes(json);
            var base64Str = Convert.ToBase64String(bytes);
            return base64Str;
        }

        /// <summary>
        /// Convert base64 object to string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        public static T? Deserialize<T>(string str)
        {
            var obj = default(T);
            if (string.IsNullOrEmpty(str)) return obj;
            byte[] bytes = Convert.FromBase64String(str);
            var json = Encoding.Default.GetString(bytes);
            var result = JsonConvert.DeserializeObject<T>(json);
            return result;
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

        private bool IsBlacklisted(string relativeFilePath)
        {
            var fileName = Path.GetFileName(relativeFilePath);
            var fileExtension = Path.GetExtension(relativeFilePath);

            return _blackFiles.Contains(fileName) || _blackFileFormats.Contains(fileExtension);
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