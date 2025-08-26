using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;

namespace GeneralUpdate.Common.Compress;

public class ZipCompressionStrategy : ICompressionStrategy
{
    /// <summary>
    /// Creates a zip archive containing the files and subdirectories of the specified directory.
    /// </summary>
    /// <param name="sourceDirectoryName">The path of the file directory to be compressed and archived, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory. </param>
    /// <param name="destinationArchiveFileName">The archive path of the compressed package to be generated, which can be a relative path or an absolute path. A relative path is a path relative to the current working directory. </param>
    /// <param name="compressionLevel">Enumeration value indicating whether the compression operation emphasizes speed or compression size .</param>
    /// <param name="includeBaseDirectory">Whether the archive contains the parent directory .</param>
    public void Compress(string sourceDirectoryName
        , string destinationArchiveFileName
        , bool includeBaseDirectory
        , Encoding encoding)
    {
        try
        {
            var compressionLevel = CompressionLevel.Optimal;
            if (Directory.Exists(sourceDirectoryName))
            {
                if (!File.Exists(destinationArchiveFileName))
                {
                    ZipFile.CreateFromDirectory(sourceDirectoryName
                        , destinationArchiveFileName
                        , compressionLevel
                        , includeBaseDirectory
                        , encoding);
                }
                else
                {
                    var toZipFileDictionaryList = GetAllDirList(sourceDirectoryName, includeBaseDirectory);
                    using var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Update, encoding);
                    foreach (var toZipFileKey in toZipFileDictionaryList.Keys)
                    {
                        if (toZipFileKey == destinationArchiveFileName) continue;

                        var toZipedFileName = Path.GetFileName(toZipFileKey);
                        var toDelArchives = new List<ZipArchiveEntry>();
                        foreach (var zipArchiveEntry in archive.Entries)
                        {
                            if (toZipedFileName != null &&
                                (zipArchiveEntry.FullName.StartsWith(toZipedFileName) || toZipedFileName.StartsWith(zipArchiveEntry.FullName)))
                            {
                                toDelArchives.Add(zipArchiveEntry);
                            }
                        }

                        foreach (var zipArchiveEntry in toDelArchives)
                        {
                            zipArchiveEntry.Delete();
                        }

                        archive.CreateEntryFromFile(toZipFileKey, toZipFileDictionaryList[toZipFileKey], compressionLevel);
                    }
                }
            }
            else if (File.Exists(sourceDirectoryName))
            {
                if (!File.Exists(destinationArchiveFileName))
                {
                    ZipFile.CreateFromDirectory(sourceDirectoryName
                        , destinationArchiveFileName
                        , compressionLevel
                        , false
                        , encoding);
                }
                else
                {
                    using var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Update, encoding);
                    if (sourceDirectoryName != destinationArchiveFileName)
                    {
                        var toZipedFileName = Path.GetFileName(sourceDirectoryName);
                        var toDelArchives = new List<ZipArchiveEntry>();
                        foreach (var zipArchiveEntry in archive.Entries)
                        {
                            if (toZipedFileName != null
                                && (zipArchiveEntry.FullName.StartsWith(toZipedFileName) || toZipedFileName.StartsWith(zipArchiveEntry.FullName)))
                            {
                                toDelArchives.Add(zipArchiveEntry);
                            }
                        }

                        foreach (var zipArchiveEntry in toDelArchives)
                        {
                            zipArchiveEntry.Delete();
                        }

                        archive.CreateEntryFromFile(sourceDirectoryName, toZipedFileName, compressionLevel);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            GeneralTracer.Error("The Compress method in the ZipCompressionStrategy class throws an exception." , exception);
            throw new Exception($"Failed to compress archive: {exception.Message}");
        }
    }

    /// <summary>
    /// Unzip the Zip file and save it to the specified target path folder .
    /// </summary>
    /// <param name="zipFilePath"></param>
    /// <param name="unZipDir"></param>
    /// <returns></returns>
    public void Decompress(string zipFilePath, string unZipDir, Encoding encoding)
    {
        try
        {
            var dirSeparatorChar = Path.DirectorySeparatorChar.ToString();
            unZipDir = unZipDir.EndsWith(dirSeparatorChar) ? unZipDir : unZipDir + dirSeparatorChar;

            var directoryInfo = new DirectoryInfo(unZipDir);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            var fileInfo = new FileInfo(zipFilePath);
            if (!fileInfo.Exists)
            {
                return;
            }

            using var zipToOpen = new FileStream(zipFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read, false, encoding);
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                var entries = archive.Entries[i];
                var pattern = $"^{dirSeparatorChar}*";
                var entryFilePath = Regex.Replace(entries.FullName.Replace("/", dirSeparatorChar), pattern,
                    "");
                if (entryFilePath.EndsWith(dirSeparatorChar))
                {
                    continue;
                }

                var filePath = directoryInfo + entryFilePath;
                var greatFolder = Directory.GetParent(filePath);
                if (greatFolder is not null && !greatFolder.Exists)
                {
                    greatFolder.Create();
                }

                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }

                entries.ExtractToFile(filePath);
            }
        }
        catch (Exception exception)
        {
            GeneralTracer.Error("The Decompress method in the ZipCompressionStrategy class throws an exception." , exception);
            throw new Exception($"Failed to decompress archive: {exception.Message}");
        }
    }

    /// <summary>
    /// Recursively get the set of all files in the specified directory on the disk, the return type is: dictionary [file name, relative file name to be compressed]
    /// </summary>
    /// <param name="strBaseDir"></param>
    /// <param name="includeBaseDirectory"></param>
    /// <param name="namePrefix"></param>
    /// <returns></returns>
    private Dictionary<string, string> GetAllDirList(string strBaseDir
        , bool includeBaseDirectory = false
        , string namePrefix = "")
    {
        var resultDictionary = new Dictionary<string, string>();
        var directoryInfo = new DirectoryInfo(strBaseDir);
        var directories = directoryInfo.GetDirectories();
        var fileInfos = directoryInfo.GetFiles();
        if (includeBaseDirectory)
            namePrefix += directoryInfo.Name + "\\";
        foreach (var directory in directories)
            resultDictionary = resultDictionary.Concat(GetAllDirList(directory.FullName, true, namePrefix))
                .ToDictionary(k => k.Key, k => k.Value);
        foreach (var fileInfo in fileInfos)
            if (!resultDictionary.ContainsKey(fileInfo.FullName))
                resultDictionary.Add(fileInfo.FullName, namePrefix + fileInfo.Name);
        return resultDictionary;
    }
}