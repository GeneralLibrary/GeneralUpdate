using GeneralUpdate.Core.Utils;
using GeneralUpdate.Differential.Binary;
using GeneralUpdate.Differential.Common;
using GeneralUpdate.Differential.ContentProvider;
using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Events;
using GeneralUpdate.Zip.Factory;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential
{
    public sealed class DifferentialCore
    {
        #region Private Members

        /// <summary>
        /// Differential file format .
        /// </summary>
        private const string PATCH_FORMAT = ".patch";
        private const string PATCHS = "patchs";

        private static readonly object _lockObj = new object();
        private static DifferentialCore _instance;

        private Action<object, BaseCompressProgressEventArgs> _compressProgressCallback;

        #endregion Private Members

        #region Constructors

        private DifferentialCore() { }

        #endregion Constructors

        #region Public Properties

        public static DifferentialCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null) _instance = new DifferentialCore();
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
        /// <param name="appPath">Previous version folder path .</param>
        /// <param name="targetPath">Recent version folder path.</param>
        /// <param name="patchPath">Store discovered incremental update files in a temporary directory .</param>
        /// <param name="compressProgressCallback">Incremental package generation progress callback function.</param>
        /// <param name="type">7z or zip</param>
        /// <param name="encoding">Incremental packet encoding format .</param>
        /// <returns></returns>
        public async Task Clean(string appPath, string targetPath, string patchPath = null, Action<object, BaseCompressProgressEventArgs> compressProgressCallback = null, OperationType type = OperationType.GZip, Encoding encoding = null,string name = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patchPath)) patchPath = Path.Combine(Environment.CurrentDirectory, PATCHS);
                if (!Directory.Exists(patchPath)) Directory.CreateDirectory(patchPath);

                //Take the left tree as the center to match the files that are not in the right tree .
                var fileProvider = new FileProvider();
                var nodes = await fileProvider.Compare(appPath, targetPath);

                //Binary differencing of like terms .
                foreach (var file in nodes.Item3)
                {
                    var dirSeparatorChar = Path.DirectorySeparatorChar.ToString().ToCharArray();
                    var tempPath = file.FullName.Replace(targetPath, "").Replace(Path.GetFileName(file.FullName), "").TrimStart(dirSeparatorChar).TrimEnd(dirSeparatorChar);
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
                    var oldfile = finOldFile == null ? "" : finOldFile.FullName;
                    var newfile = file.FullName;
                    var extensionName = Path.GetExtension(file.FullName);
                    if (File.Exists(oldfile) && File.Exists(newfile) && !Filefilter.Diff.Contains(extensionName))
                    {
                        //Generate the difference file to the difference directory .
                        await new BinaryHandle().Clean(oldfile, newfile, tempPath0);
                    }
                    else
                    {
                        File.Copy(newfile, Path.Combine(tempDir, Path.GetFileName(newfile)), true);
                    }
                }
                var factory = new GeneralZipFactory();
                _compressProgressCallback = compressProgressCallback;
                if (_compressProgressCallback != null) factory.CompressProgress += OnCompressProgress;
                //The update package exists in the 'target path' directory.
                name = name ?? new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString();
                factory.CreatefOperate(type, name, patchPath, targetPath, true, encoding).CreatZip();
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
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Drity(string appPath, string patchPath)
        {
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;
            try
            {
                if (string.IsNullOrWhiteSpace(patchPath) || string.IsNullOrWhiteSpace(appPath))
                    throw new ArgumentNullException(nameof(appPath));

                var patchFiles = FileUtil.GetAllFiles(patchPath);
                var oldFiles = FileUtil.GetAllFiles(appPath);
                foreach (var oldFile in oldFiles)
                {
                    //Only the difference file (.patch) can be updated here.
                    var findFile = patchFiles.FirstOrDefault(f => 
                    {
                        var tempName = Path.GetFileNameWithoutExtension(f.Name).Replace(PATCH_FORMAT, "");
                        return tempName.Equals(oldFile.Name);
                    });
                    if (findFile != null)
                    {
                        var extensionName = Path.GetExtension(findFile.FullName);
                        if (!extensionName.Equals(PATCH_FORMAT)) continue;
                        await DrityPatch(oldFile.FullName, findFile.FullName);
                    }
                }
                //Update does not include files or copies configuration files.
                await DirtyUnknow(appPath, patchPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Drity error : {ex.Message} !", ex.InnerException);
            }
        }

        /// <summary>
        /// Apply patch file .
        /// </summary>
        /// <param name="appPath">Client application directory .</param>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task DrityPatch(string appPath, string patchPath)
        {
            try
            {
                if (!File.Exists(appPath) || !File.Exists(patchPath)) return;
                var newPath = Path.Combine(Path.GetDirectoryName(appPath), $"{Path.GetRandomFileName()}_{Path.GetFileName(appPath)}");
                await new BinaryHandle().Drity(appPath, newPath, patchPath);
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
        /// <param name="patchPath"></param>
        private Task DirtyUnknow(string appPath, string patchPath)
        {
            try
            {
                var listExcept = FileUtil.Compare(patchPath, appPath);
                foreach (var file in listExcept)
                {
                    //var extensionName = Path.GetExtension(file.FullName);
                    //if (Filefilter.Diff.Contains(extensionName)) continue;
                    var targetName = file.FullName.Replace(patchPath, "").TrimStart("\\".ToCharArray());
                    var targetPath = Path.Combine(appPath, targetName);
                    var greatFolder = Directory.GetParent(targetPath);
                    if (!greatFolder.Exists) greatFolder.Create();
                    File.Copy(file.FullName, Path.Combine(appPath, targetPath), true);
                }
                if (Directory.Exists(patchPath)) Directory.Delete(patchPath, true);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new Exception($" DrityNew error : {ex.Message} !", ex.InnerException);
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void OnCompressProgress(object sender, BaseCompressProgressEventArgs e) => _compressProgressCallback(sender, e);

        #endregion Private Methods
    }
}