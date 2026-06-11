using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeneralUpdate.Core.Compress;

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
                            // Exact match to avoid ambiguity (e.g. "foo.dll" should not match "foobar.dll").
                            // For directory entries, also match "subdir/" prefix so nested files are replaced.
                            if (toZipedFileName != null &&
                                (zipArchiveEntry.FullName == toZipedFileName ||
                                 zipArchiveEntry.FullName.StartsWith(toZipedFileName + "/", StringComparison.Ordinal) ||
                                 toZipedFileName.StartsWith(zipArchiveEntry.FullName, StringComparison.Ordinal)))
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
                                && (zipArchiveEntry.FullName == toZipedFileName ||
                                    zipArchiveEntry.FullName.StartsWith(toZipedFileName + "/", StringComparison.Ordinal) ||
                                    toZipedFileName.StartsWith(zipArchiveEntry.FullName, StringComparison.Ordinal)))
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

            var extractionRoot = Path.GetFullPath(unZipDir);

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

                // Guard against path-traversal entries (e.g. "../../evil.exe")
                var filePath = directoryInfo + entryFilePath;
                var fullTargetPath = Path.GetFullPath(filePath);
                if (!fullTargetPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Zip entry path traversal detected: {entries.FullName} resolves outside extraction directory.");

                var greatFolder = Directory.GetParent(fullTargetPath);
                if (greatFolder is not null && !greatFolder.Exists)
                {
                    greatFolder.Create();
                }

                if (File.Exists(fullTargetPath))
                {
                    File.SetAttributes(fullTargetPath, FileAttributes.Normal);
                    File.Delete(fullTargetPath);
                }

                entries.ExtractToFile(fullTargetPath);
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