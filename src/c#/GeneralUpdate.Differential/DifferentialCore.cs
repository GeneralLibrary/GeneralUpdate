using GeneralUpdate.Core.ContentProvider;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Differential.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential
{
    public sealed class DifferentialCore
    {
        #region Private Members

        private static readonly object _lockObj = new object();
        private static DifferentialCore _instance;

        /// <summary>
        /// Differential file format .
        /// </summary>
        private const string PATCH_FORMAT = ".patch";

        /// <summary>
        /// Patch catalog.
        /// </summary>
        private const string PATCHS = "patchs";

        /// <summary>
        /// List of files that need to be deleted.
        /// </summary>
        private const string DELETE_FILES_NAME = "generalupdate_delete_files.json";

        #endregion Private Members

        #region Public Properties

        public static DifferentialCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                        {
                            _instance = new DifferentialCore();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Generate patch file [Cannot contain files with the same name but different extensions] .
        /// </summary>
        /// <param name="sourcePath">Previous version folder path .</param>
        /// <param name="targetPath">Recent version folder path.</param>
        /// <param name="patchPath">Store discovered incremental update files in a temporary directory .</param>
        /// <returns></returns>
        public async Task Clean(string sourcePath, string targetPath, string patchPath = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patchPath)) 
                    patchPath = Path.Combine(Environment.CurrentDirectory, PATCHS);
                if (!Directory.Exists(patchPath))
                    Directory.CreateDirectory(patchPath);

                //Take the left tree as the center to match the files that are not in the right tree .
                var fileProvider = new FileProvider();
                var nodes = await fileProvider.Compare(sourcePath, targetPath);
                var hashAlgorithm = new Sha256HashAlgorithm();

                //Binary differencing of like terms .
                foreach (var file in nodes.Item3)
                {
                    var tempPath = file.FullName
                        .Replace(targetPath, string.Empty)
                        .Replace(Path.GetFileName(file.FullName), string.Empty)
                        .Trim(Path.DirectorySeparatorChar);
                    var tempPath0 = string.Empty;
                    var tempDir = string.Empty;
                    if (string.IsNullOrEmpty(tempPath))
                    {
                        tempDir = patchPath;
                        tempPath0 = Path.Combine(patchPath, $"{file.Name}{PATCH_FORMAT}");
                    }
                    else
                    {
                        tempDir = Path.Combine(patchPath, tempPath);
                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                        tempPath0 = Path.Combine(tempDir, $"{file.Name}{PATCH_FORMAT}");
                    }
                    
                    var finOldFile = nodes.Item1.FirstOrDefault(i => i.Name.Equals(file.Name));
                    var oldFile = finOldFile == null ? string.Empty : finOldFile.FullName;
                    var newFile = file.FullName;
                    var extensionName = Path.GetExtension(file.FullName);
                    if (File.Exists(oldFile) && File.Exists(newFile) && !FileProvider.GetBlackFileFormats().Contains(extensionName))
                    {
                        if (hashAlgorithm.ComputeHash(oldFile)
                            .Equals(hashAlgorithm.ComputeHash(newFile), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        //Generate the difference file to the difference directory .
                        await new BinaryHandle().Clean(oldFile, newFile, tempPath0);
                    }
                    else
                    {
                        File.Copy(newFile, Path.Combine(tempDir, Path.GetFileName(newFile)), true);
                    }
                }

                //If a file is found that needs to be deleted, a list of files is written to the update package.
                var exceptFiles = (await fileProvider.Except(sourcePath, targetPath)).ToList();
                if (exceptFiles.Count != 0)
                {
                    var path = Path.Combine(patchPath, DELETE_FILES_NAME);
                    FileProvider.CreateJson(path, exceptFiles);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Generate error : {ex.Message} !", ex.InnerException);
            }
        }

        /// <summary>
        /// Apply patch [Cannot contain files with the same name but different extensions] .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath">Patch file path.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Dirty(string appPath, string patchPath)
        {
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;
            try
            {
                var patchFiles = FileProvider.GetAllfiles(patchPath);
                var oldFiles = FileProvider.GetAllfiles(appPath);

                //If a JSON file for the deletion list is found in the update package, it will be deleted based on its contents.
                var deleteListJson = patchFiles.FirstOrDefault(i => i.Name.Equals(DELETE_FILES_NAME));
                if (deleteListJson != null)
                {
                    var deleteFiles = FileProvider.GetJson<IEnumerable<FileNode>>(deleteListJson.FullName);
                    var hashAlgorithm = new Sha256HashAlgorithm();
                    foreach (var file in deleteFiles)
                    {
                        var resultFile = oldFiles.Find(i => 
                            string.Equals(hashAlgorithm.ComputeHash(i.FullName), file.Hash, StringComparison.OrdinalIgnoreCase));
                        if (resultFile == null)
                        {
                            continue;
                        }
                        if (File.Exists(resultFile.FullName))
                        {
                            File.Delete(resultFile.FullName);
                        }
                    }
                }

                foreach (var oldFile in oldFiles)
                {
                    //Only the difference file (.patch) can be updated here.
                    var findFile = patchFiles.Find(f =>
                    {
                        var tempName = Path.GetFileNameWithoutExtension(f.Name).Replace(PATCH_FORMAT, "");
                        return tempName.Equals(oldFile.Name);
                    });
                    if (findFile != null)
                    {
                        var extensionName = Path.GetExtension(findFile.FullName);
                        if (!extensionName.Equals(PATCH_FORMAT)) continue;
                        await DirtyPatch(oldFile.FullName, findFile.FullName);
                    }
                }
                //Update does not include files or copies configuration files.
                await DirtyUnknow(appPath, patchPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Dirty error : {ex.Message} !", ex.InnerException);
            }
        }

        /// <summary>
        /// Set a blacklist.
        /// </summary>
        /// <param name="blackFiles">A collection of blacklist files that are skipped when updated.</param>
        /// <param name="blackFileFormats">A collection of blacklist file name extensions that are skipped on update.</param>
        public void SetBlocklist(List<string> blackFiles, List<string> blackFileFormats) => FileProvider.SetBlacklist(blackFiles, blackFileFormats);

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Apply patch file .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task DirtyPatch(string appPath, string patchPath)
        {
            try
            {
                if (!File.Exists(appPath) || !File.Exists(patchPath)) return;
                var newPath = Path.Combine(Path.GetDirectoryName(appPath), $"{Path.GetRandomFileName()}_{Path.GetFileName(appPath)}");
                await new BinaryHandle().Dirty(appPath, newPath, patchPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"RevertFile error : {ex.Message} !", ex.InnerException);
            }
        }

        /// <summary>
        /// Add new files .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath">Patch file path.</param>
        private Task DirtyUnknow(string appPath, string patchPath)
        {
            try
            {
                var fileProvider = new FileProvider();
                var listExcept = fileProvider.Comparer(appPath, patchPath);
                foreach (var file in listExcept)
                {
                    var extensionName = Path.GetExtension(file.FullName);
                    if (FileProvider.GetBlackFileFormats().Contains(extensionName)) continue;
                    var targetFileName = file.FullName.Replace(patchPath, "").TrimStart("\\".ToCharArray());
                    var targetPath = Path.Combine(appPath, targetFileName);
                    var parentFolder = Directory.GetParent(targetPath);
                    if (!parentFolder.Exists) parentFolder.Create();
                    File.Copy(file.FullName, Path.Combine(appPath, targetPath), true);
                }
                if (Directory.Exists(patchPath)) Directory.Delete(patchPath, true);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new Exception($" DirtyNew error : {ex.Message} !", ex.InnerException);
            }
        }

        #endregion Private Methods
    }
}