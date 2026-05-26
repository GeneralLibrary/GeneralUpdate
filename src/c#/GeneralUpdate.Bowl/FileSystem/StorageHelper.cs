using System;
using System.IO;
using System.Text.Json;

namespace GeneralUpdate.Bowl.FileSystem;

/// <summary>
/// Minimal file system utilities for Bowl (backup restore, directory cleanup, JSON serialization).
/// </summary>
public static class StorageHelper
{
    public static void Restore(string backupPath, string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
            Directory.CreateDirectory(sourcePath);
        CopyDirectory(backupPath, sourcePath);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            string newTargetDir = Path.Combine(targetDir, Path.GetFileName(dirPath));
            Directory.CreateDirectory(newTargetDir);
            CopyDirectory(dirPath, newTargetDir);
        }

        foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly))
        {
            string newFilePath = Path.Combine(targetDir, Path.GetFileName(filePath));
            File.Copy(filePath, newFilePath, true);
        }
    }

    public static void DeleteDirectory(string targetDir)
    {
        foreach (var file in Directory.GetFiles(targetDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(targetDir))
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }

    public static void CreateJson<T>(string targetPath, T obj) where T : class
    {
        var folderPath = Path.GetDirectoryName(targetPath) ??
                         throw new ArgumentException("invalid path", nameof(targetPath));

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var jsonString = JsonSerializer.Serialize(obj);
        File.WriteAllText(targetPath, jsonString);
    }
}
