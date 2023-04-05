using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GeneralUpdate.Core.Utils
{
    public static class FileUtil
    {
        public static string GetFileMD5(string fileName)
        {
            try
            {
                var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var md5 = new MD5CryptoServiceProvider();
                var retVal = md5.ComputeHash(file);
                file.Close();
                var sb = new StringBuilder();
                for (var i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName,
          bool copySubDirs, bool isOverWrite, Action<string> action)
        {
            var dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists) return;
            var dirs = dir.GetDirectories();
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }
            foreach (var file in dir.GetFiles())
            {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, isOverWrite);
                if (action != null) action.Invoke(file.Name);
            }
            if (copySubDirs)
            {
                foreach (var subdir in dirs)
                {
                    var temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, true, isOverWrite, action);
                }
            }
            Directory.Delete(sourceDirName, true);
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

        /// <summary>
        /// Compare the contents of two folders for equality.
        /// </summary>
        /// <param name="folder1">source path</param>
        /// <param name="folder2">target path</param>
        /// <returns>item1 :  The following files are in both folders, item2 : The following files are in list1 but not list2.</returns>
        public static IEnumerable<FileInfo> Compare(string folder1, string folder2)
        {
            // Create two identical or different temporary folders
            // on a local drive and change these file paths.
            var dir1 = new DirectoryInfo(folder1);
            var dir2 = new DirectoryInfo(folder2);

            // Take a snapshot of the file system.
            var list1 = dir1.GetFiles("*.*", SearchOption.AllDirectories);
            var list2 = dir2.GetFiles("*.*", SearchOption.AllDirectories);

            //A custom file comparer defined below
            var fileCompare = new FileCompare();

            // This query determines whether the two folders contain
            // identical file lists, based on the custom file comparer
            // that is defined in the FileCompare class.
            // The query executes immediately because it returns a bool.
            //areIdentical  true : the two folders are the same; false : The two folders are not the same.
            var areIdentical = list1.SequenceEqual(list2, fileCompare);

            // Find the common files. It produces a sequence and doesn't
            // execute until the foreach statement.
            // list1.Intersect(list2, fileCompare);

            // Find the set difference between the two folders.
            // For this example we only check one way.
            //The following files are in list1 but not list2
            // (from file in list1 select file).Except(list2, fileCompare);
            var result = (from file in list1 select file).Except(list2, fileCompare);
            return (from file in list1 select file).Except(list2, fileCompare);
        }

        public static FileInfo[] GetAllFiles(string path)
        {
            try
            {
                var files = new List<FileInfo>();
                files.AddRange(new DirectoryInfo(path).GetFiles());
                var tmpdics = new DirectoryInfo(path).GetDirectories();
                foreach (var dic in tmpdics)
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

        public static void CreateJsonFile<T>(string targetPath,string fileName,T obj) 
        {
            var fileFullPath = Path.Combine(targetPath,fileName);
            if (File.Exists(fileFullPath)) File.Delete(fileFullPath);
            var jsonString = JsonConvert.SerializeObject(obj);
            File.WriteAllText(fileFullPath, jsonString);
        }

        public static T ReadJsonFile<T>(string path)
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
    }

    /// <summary>
    /// This implementation defines a very simple comparison
    /// between two FileInfo objects. It only compares the name
    /// of the files being compared and their length in bytes.
    /// </summary>
    public class FileCompare : IEqualityComparer<FileInfo>
    {
        public FileCompare()
        { }

        public bool Equals(FileInfo f1, FileInfo f2)
        {
            return (f1.Name == f2.Name &&
                    f1.Length == f2.Length);
        }

        // Return a hash that reflects the comparison criteria. According to the
        // rules for IEqualityComparer<T>, if Equals is true, then the hash codes must
        // also be equal. Because equality as defined here is a simple value equality, not
        // reference identity, it is possible that two or more objects will produce the same
        // hash code.
        public int GetHashCode(FileInfo fi)
        {
            string s = $"{fi.Name}{fi.Length}";
            return s.GetHashCode();
        }
    }
}