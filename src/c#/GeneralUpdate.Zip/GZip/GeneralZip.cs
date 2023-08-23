using GeneralUpdate.Zip.Events;
using GeneralUpdate.Zip.Factory;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeneralUpdate.Zip.GZip
{
    /// <summary>
    /// Source address  : https://www.cnblogs.com/Chary/p/No0000DF.html
    /// Author ：Chary Gao .
    /// Tribute to the original author .
    /// Secondary developer : Juster Z
    /// </summary>
    public class GeneralZip : BaseCompress
    {
        private string _destinationPath;
        private bool _includeBaseDirectory;
        private Encoding _encoding;

        /// <summary>
        /// Creates a zip archive containing the files and subdirectories of the specified directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The path of the file directory to be compressed and archived, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory. </param>
        /// <param name="destinationArchiveFileName">The archive path of the compressed package to be generated, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory. </param>
        /// <param name="compressionLevel">Enumeration value indicating whether the compression operation emphasizes speed or compression size .</param>
        /// <param name="includeBaseDirectory">Whether the archive contains the parent directory .</param>
        public bool CreateZip(string sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel = CompressionLevel.NoCompression, bool includeBaseDirectory = true)
        {
            int i = 1;
            try
            {
                if (Directory.Exists(sourceDirectoryName))
                    if (!File.Exists(destinationArchiveFileName))
                    {
                        ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, includeBaseDirectory);
                    }
                    else
                    {
                        var toZipFileDictionaryList = GetAllDirList(sourceDirectoryName, includeBaseDirectory);
                        using (var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Update))
                        {
                            var count = toZipFileDictionaryList.Keys.Count;
                            foreach (var toZipFileKey in toZipFileDictionaryList.Keys)
                            {
                                if (toZipFileKey != destinationArchiveFileName)
                                {
                                    var toZipedFileName = Path.GetFileName(toZipFileKey);
                                    var toDelArchives = new List<ZipArchiveEntry>();
                                    foreach (var zipArchiveEntry in archive.Entries)
                                    {
                                        if (toZipedFileName != null && (zipArchiveEntry.FullName.StartsWith(toZipedFileName) || toZipedFileName.StartsWith(zipArchiveEntry.FullName)))
                                        {
                                            i++;
                                            OnCompressProgressEventHandler(this, new BaseCompressProgressEventArgs { Size = zipArchiveEntry.Length, Count = count, Index = i, Path = zipArchiveEntry.FullName, Name = zipArchiveEntry.Name });
                                            toDelArchives.Add(zipArchiveEntry);
                                        }
                                    }

                                    foreach (var zipArchiveEntry in toDelArchives)
                                        zipArchiveEntry.Delete();
                                    archive.CreateEntryFromFile(toZipFileKey, toZipFileDictionaryList[toZipFileKey], compressionLevel);
                                }
                            }
                        }
                    }
                else if (File.Exists(sourceDirectoryName))
                    if (!File.Exists(destinationArchiveFileName))
                        ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, false);
                    else
                    {
                        using (var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Update))
                        {
                            if (sourceDirectoryName != destinationArchiveFileName)
                            {
                                var toZipedFileName = Path.GetFileName(sourceDirectoryName);
                                var toDelArchives = new List<ZipArchiveEntry>();
                                var count = archive.Entries.Count;
                                foreach (var zipArchiveEntry in archive.Entries)
                                {
                                    if (toZipedFileName != null && (zipArchiveEntry.FullName.StartsWith(toZipedFileName) || toZipedFileName.StartsWith(zipArchiveEntry.FullName)))
                                    {
                                        i++;
                                        OnCompressProgressEventHandler(this, new BaseCompressProgressEventArgs { Size = zipArchiveEntry.Length, Count = count, Index = i, Path = zipArchiveEntry.FullName, Name = zipArchiveEntry.Name });
                                        toDelArchives.Add(zipArchiveEntry);
                                    }
                                }

                                foreach (var zipArchiveEntry in toDelArchives)
                                    zipArchiveEntry.Delete();
                                archive.CreateEntryFromFile(sourceDirectoryName, toZipedFileName, compressionLevel);
                            }
                        }
                    }
                else
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a zip archive containing the files and directories of the specified directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The path of the file directory to be compressed and archived, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory.</param>
        /// <param name="destinationArchiveFileName">The archive path of the compressed package to be generated, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory.</param>
        /// <param name="compressionLevel">Enumeration value indicating whether the compression operation emphasizes speed or compression size.</param>
        public bool CreatZip(Dictionary<string, string> sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel = CompressionLevel.NoCompression)
        {
            int i = 1;
            try
            {
                using (FileStream zipToOpen = new FileStream(destinationArchiveFileName, FileMode.OpenOrCreate))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (var toZipFileKey in sourceDirectoryName.Keys)
                        {
                            if (toZipFileKey != destinationArchiveFileName)
                            {
                                var toZipedFileName = Path.GetFileName(toZipFileKey);
                                var toDelArchives = new List<ZipArchiveEntry>();
                                var count = archive.Entries.Count;
                                foreach (var zipArchiveEntry in archive.Entries)
                                {
                                    if (toZipedFileName != null && (zipArchiveEntry.FullName.StartsWith(toZipedFileName) || toZipedFileName.StartsWith(zipArchiveEntry.FullName)))
                                    {
                                        i++;
                                        OnCompressProgressEventHandler(this, new BaseCompressProgressEventArgs { Size = zipArchiveEntry.Length, Count = count, Index = i, Path = toZipedFileName });
                                        toDelArchives.Add(zipArchiveEntry);
                                    }
                                }

                                foreach (var zipArchiveEntry in toDelArchives)
                                    zipArchiveEntry.Delete();
                                archive.CreateEntryFromFile(toZipFileKey, sourceDirectoryName[toZipFileKey], compressionLevel);
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Recursively delete the specified folder directory and file on the disk.
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <returns></returns>
        private bool DeleteFolder(string baseDirectory)
        {
            var successed = true;
            try
            {
                if (Directory.Exists(baseDirectory))
                {
                    foreach (var directory in Directory.GetFileSystemEntries(baseDirectory))
                        if (File.Exists(directory))
                            File.Delete(directory);
                        else
                            successed = DeleteFolder(directory);
                    Directory.Delete(baseDirectory);
                }
            }
            catch
            {
                successed = false;
            }
            return successed;
        }

        /// <summary>
        /// Recursively get the set of all files in the specified directory on the disk, the return type is: dictionary [file name, relative file name to be compressed]
        /// </summary>
        /// <param name="strBaseDir"></param>
        /// <param name="includeBaseDirectory"></param>
        /// <param name="namePrefix"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetAllDirList(string strBaseDir, bool includeBaseDirectory = false, string namePrefix = "")
        {
            var resultDictionary = new Dictionary<string, string>();
            var directoryInfo = new DirectoryInfo(strBaseDir);
            var directories = directoryInfo.GetDirectories();
            var fileInfos = directoryInfo.GetFiles();
            if (includeBaseDirectory)
                namePrefix += directoryInfo.Name + "\\";
            foreach (var directory in directories)
                resultDictionary = resultDictionary.Concat(GetAllDirList(directory.FullName, true, namePrefix)).ToDictionary(k => k.Key, k => k.Value);
            foreach (var fileInfo in fileInfos)
                if (!resultDictionary.ContainsKey(fileInfo.FullName)) resultDictionary.Add(fileInfo.FullName, namePrefix + fileInfo.Name);
            return resultDictionary;
        }

        /// <summary>
        /// Unzip the Zip file and save it to the specified target path folder .
        /// </summary>
        /// <param name="zipFilePath"></param>
        /// <param name="unZipDir"></param>
        /// <returns></returns>
        public bool UnZip(string zipFilePath, string unZipDir)
        {
            bool isComplete = false;
            try
            {
                unZipDir = unZipDir.EndsWith(@"\") ? unZipDir : unZipDir + @"\";
                var directoryInfo = new DirectoryInfo(unZipDir);
                if (!directoryInfo.Exists) directoryInfo.Create();
                var fileInfo = new FileInfo(zipFilePath);
                if (!fileInfo.Exists) return false;
                using (var zipToOpen = new FileStream(zipFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read, false, _encoding))
                    {
                        var count = archive.Entries.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var entries = archive.Entries[i];
                            if (!entries.FullName.EndsWith("/"))
                            {
                                var entryFilePath = Regex.Replace(entries.FullName.Replace("/", @"\"), @"^\\*", "");
                                var filePath = directoryInfo + entryFilePath;
                                OnUnZipProgressEventHandler(this, new BaseUnZipProgressEventArgs { Size = entries.Length, Count = count, Index = i + 1, Path = entries.FullName, Name = entries.Name });
                                var greatFolder = Directory.GetParent(filePath);
                                if (!greatFolder.Exists) greatFolder.Create();
                                //ADAPTS to .NET 6 and above decompression updates.
                                entries.ExtractToFile(filePath);

                                //var content = new byte[entries.Length];
                                //entries.Open().Read(content, 0, content.Length);
                                //File.WriteAllBytes(filePath, content);
                            }
                        }
                    }
                }
                isComplete = true;
            }
            catch
            {
                isComplete = false;
            }
            finally
            {
                OnCompletedEventHandler(this, new BaseCompleteEventArgs(isComplete));
            }
            return isComplete;
        }

        /// <summary>
        /// Get a list of files in a Zip archive .
        /// </summary>
        /// <param name="zipFilePath">The physical path of the Zip archive file.</param>
        /// <returns></returns>
        public List<string> GetZipFileList(string zipFilePath)
        {
            List<string> fList = new List<string>();
            if (!File.Exists(zipFilePath)) return fList;
            try
            {
                using (var zipToOpen = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                    {
                        foreach (var zipArchiveEntry in archive.Entries)
                            if (!zipArchiveEntry.FullName.EndsWith("/")) fList.Add(Regex.Replace(zipArchiveEntry.FullName.Replace("/", @"\"), @"^\\*", ""));
                    }
                }
            }
            catch
            {
                return null;
            }
            return fList;
        }

        public override bool CreateZip() => CreateZip(SOURSE_PATH, Path.Combine(_destinationPath, COMPRESS_NAME), CompressionLevel.Optimal, _includeBaseDirectory);

        public override bool UnZip() => UnZip(SOURSE_PATH, _destinationPath);

        public override void Configs(string name, string sourcePath, string destinationPath, Encoding encoding, bool includeBaseDirectory = false)
        {
            _encoding = encoding;
            SOURSE_PATH = sourcePath;
            COMPRESS_NAME = $"{name}.zip";
            _destinationPath = string.IsNullOrWhiteSpace(destinationPath) ? SOLUTION_BASE_PATH : destinationPath;
            _includeBaseDirectory = includeBaseDirectory;
            Verifypath(sourcePath, destinationPath);
        }
    }
}