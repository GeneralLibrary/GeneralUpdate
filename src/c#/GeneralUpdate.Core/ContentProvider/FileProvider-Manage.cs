using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.ContentProvider
{
    public partial class FileProvider
    {
        public static string GetTempDirectory(string name)
        {
            var path = $"generalupdate_{DateTime.Now.ToString("yyyy-MM-dd")}_{name}";
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