using GeneralUpdate.Differential.Binary;
using GeneralUpdate.Differential.Matchers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.HashAlgorithms;
using GeneralUpdate.Common.Internal.JsonContext;

namespace GeneralUpdate.Differential
{
    public sealed class DifferentialCore
    {
        private static readonly object _lockObj = new ();
        private static DifferentialCore? _instance;
        private const string PATCH_FORMAT = ".patch";
        private const string DELETE_FILES_NAME = "generalupdate_delete_files.json";

        public static DifferentialCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        _instance ??= new DifferentialCore();
                    }
                }
                return _instance;
            }
        }

        public async Task Clean(string sourcePath, string targetPath, string patchPath, ICleanMatcher? matcher = null)
        {
            matcher ??= new DefaultCleanMatcher();
            var fileManager = new StorageManager();
            var comparisonResult = fileManager.Compare(sourcePath, targetPath);
            foreach (var file in comparisonResult.DifferentNodes)
            {
                var tempDir = GetTempDirectory(file, targetPath, patchPath);
                var oldFile = matcher.Match(file, comparisonResult.LeftNodes);
                var newFile = file;

                if (oldFile is not null)
                {
                    if (!StorageManager.HashEquals(oldFile.FullName, newFile.FullName))
                    {
                        var tempPatchPath = Path.Combine(tempDir, $"{file.Name}{PATCH_FORMAT}");
                        await new BinaryHandler().Clean(oldFile.FullName, newFile.FullName, tempPatchPath);
                    }
                }
                else
                {
                    File.Copy(newFile.FullName, Path.Combine(tempDir, Path.GetFileName(newFile.FullName)), true);
                }
            }

            var exceptFiles = fileManager.Except(sourcePath, targetPath);
            if (exceptFiles is not null
                && exceptFiles.Any())
            {
                var path = Path.Combine(patchPath, DELETE_FILES_NAME);
                StorageManager.CreateJson(path, exceptFiles);
            }
        }
        
        public async Task Dirty(string appPath, string patchPath, IDirtyMatcher? matcher = null)
        {
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

            matcher ??= new DefaultDirtyMatcher();
            var skipDirectory = BlackListManager.Instance.SkipDirectorys.ToList();
            var patchFiles = StorageManager.GetAllFiles(patchPath, skipDirectory);
            var oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory);
            //Refresh the collection after deleting the file.
            HandleDeleteList(patchFiles, oldFiles);
            oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory);
            foreach (var oldFile in oldFiles)
            {
                var findFile = matcher.Match(oldFile, patchFiles);
                if (findFile != null)
                {
                    await DirtyPatch(oldFile.FullName, findFile.FullName);
                }
            }

            await DirtyUnknow(appPath, patchPath);
        }

        #region Private Methods

        private static string GetTempDirectory(FileNode file, string targetPath, string patchPath)
        {
            var tempPath = file.FullName.Replace(targetPath, "").Replace(Path.GetFileName(file.FullName), "").Trim(Path.DirectorySeparatorChar);
            var tempDir = string.IsNullOrEmpty(tempPath) ? patchPath : Path.Combine(patchPath, tempPath);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }
        
        private void HandleDeleteList(IEnumerable<FileInfo> patchFiles, IEnumerable<FileInfo> oldFiles)
        {
            var json = patchFiles.FirstOrDefault(i => i.Name.Equals(DELETE_FILES_NAME));
            if (json == null)
                return;
            
            var deleteFiles = StorageManager.GetJson<List<FileNode>>(json.FullName, FileNodesJsonContext.Default.ListFileNode);
            if (deleteFiles == null)
                return;
            
            //Match the collection of files to be deleted based on the file hash values stored in the JSON file.
            var hashAlgorithm = new Sha256HashAlgorithm();
            var tempDeleteFiles = oldFiles.Where(old => deleteFiles.Any(del => del.Hash.SequenceEqual(hashAlgorithm.ComputeHash(old.FullName)))).ToList();
            foreach (var file in tempDeleteFiles)
            {
                if (!File.Exists(file.FullName))
                    continue;
                
                File.SetAttributes(file.FullName, FileAttributes.Normal);
                File.Delete(file.FullName);
            }
        }

        private async Task DirtyPatch(string appPath, string patchPath)
        {
            if (!File.Exists(appPath) || !File.Exists(patchPath))
                return;

            var newPath = Path.Combine(Path.GetDirectoryName(appPath)!, $"{Path.GetRandomFileName()}_{Path.GetFileName(appPath)}");
            await new BinaryHandler().Dirty(appPath, newPath, patchPath);
        }

        private async Task DirtyUnknow(string appPath, string patchPath)
        {
            await Task.Run(() =>
            {
                var fileManager = new StorageManager();
                var comparisonResult = fileManager.Compare(appPath, patchPath);
                foreach (var file in comparisonResult.DifferentNodes)
                {
                    var extensionName = Path.GetExtension(file.FullName);
                    if (BlackListManager.Instance.IsBlacklisted(extensionName)) continue;

                    var targetFileName = file.FullName.Replace(patchPath, "").TrimStart(Path.DirectorySeparatorChar);
                    var targetPath = Path.Combine(appPath, targetFileName);
                    var parentFolder = Directory.GetParent(targetPath);
                    if (parentFolder?.Exists == false)
                    {
                        parentFolder.Create();
                    }

                    File.Copy(file.FullName, targetPath, true);
                }

                if (Directory.Exists(patchPath))
                {
                    StorageManager.DeleteDirectory(patchPath);
                }
            });
        }

        #endregion
    }
}