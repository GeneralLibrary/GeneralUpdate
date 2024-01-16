using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.Utils
{
    public static class FileUtil
    {
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

        public static void CreateJson<T>(string targetPath,T obj) 
        {
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            var jsonString = JsonConvert.SerializeObject(obj);
            File.WriteAllText(targetPath, jsonString);
        }

        public static T GetJson<T>(string path)
        {
            if (File.Exists(path)) 
            {
               var json = File.ReadAllText(path);
                if (json != null) 
                {
                   return  JsonConvert.DeserializeObject<T>(json);
                }
            }
            return default(T);
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