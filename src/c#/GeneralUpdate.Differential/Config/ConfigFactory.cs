using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Differential.Common;
using GeneralUpdate.Differential.Config.Cache;
using GeneralUpdate.Differential.Config.Handles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Config
{
    /// <summary>
    /// Update local configuration file.[Currently only files with a depth of 1 are supported.]
    /// Currently only json files are supported.
    /// </summary>
    public sealed class ConfigFactory : IDisposable
    {
        #region Private Members

        private ConfigCache<ConfigEntity> _configCache;
        private string _appPath, _scanPath;
        private List<string> _files;
        private bool _disposed = false;
        private static readonly object _locker = new object();
        private static ConfigFactory _instance;

        #endregion Private Members

        #region Constructors

        private ConfigFactory()
        {
            _configCache = new ConfigCache<ConfigEntity>();
        }

        ~ConfigFactory()
        {
            Dispose();
        }

        #endregion Constructors

        #region Public Properties

        public static ConfigFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_locker)
                    {
                        if (_instance == null) _instance = new ConfigFactory();
                    }
                }
                return _instance;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Deploy configuration file.
        /// </summary>
        public async Task Deploy()
        {
            try
            {
                if (_configCache.Cache != null)
                {
                    foreach (var cacheItem in _configCache.Cache)
                    {
                        var value = cacheItem.Value;
                        if (value == null) continue;
                        var hashAlgorithm = new Sha256HashAlgorithm();
                        var hash = hashAlgorithm.ComputeHash(value.OldPath);
                        var oldEntity = await Handle(value.OldPath, hash);
                        await InitHandle<ConfigEntity>(value.Handle).Write(oldEntity, value);
                    }
                    Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Deploy config error : {ex.Message} .", ex.InnerException);
            }
        }

        /// <summary>
        /// Scan configuration files and cache, backup.
        /// </summary>
        /// <param name="appPath">The application directory that needs to be updated.</param>
        /// <param name="scanPath">update package directory.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Scan(string appPath = null, string scanPath = null)
        {
            try
            {
                _disposed = false;
                _appPath = appPath ?? Environment.CurrentDirectory;
                _scanPath = scanPath ?? Environment.CurrentDirectory;
                if (!Directory.Exists(_appPath) || !Directory.Exists(_scanPath)) return;
                List<string> files = new List<string>();
                Find(_scanPath, ref files);
                if (files.Count == 0) return;
                await Cache(_files = files);
            }
            catch (Exception ex)
            {
                throw new Exception($"Scan config files error : {ex.Message} .", ex.InnerException);
            }
        }

        /// <summary>
        /// release all resources.
        /// </summary>
        /// <exception cref="Exception">dispose exception</exception>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_configCache != null)
                {
                    _configCache.Dispose();
                    _configCache = null;
                }
                if (_files != null)
                {
                    _files.Clear();
                    _files = null;
                }
                _disposed = true;
            }
            catch (Exception ex)
            {
                _disposed = false;
                throw new Exception($"'Dispose' error :{ex.Message} .", ex.InnerException);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Find matching files recursively.
        /// </summary>
        /// <param name="directory">root directory</param>
        /// <param name="files">result file list</param>
        private void Find(string rootDirectory, ref List<string> files)
        {
            var rootDirectoryInfo = new DirectoryInfo(rootDirectory);
            foreach (var file in rootDirectoryInfo.GetFiles())
            {
                var extensionName = Path.GetExtension(file.Name);
                if (!Filefilter.GetBlackFileFormats().Contains(extensionName)) continue;
                var fullName = file.FullName;
                files.Add(fullName);
            }
            foreach (var dir in rootDirectoryInfo.GetDirectories())
            {
                Find(dir.FullName, ref files);
            }
        }

        /// <summary>
        /// All resources are cached and backed up.
        /// </summary>
        /// <param name="files"></param>
        /// <exception cref="Exception"></exception>
        private async Task Cache(IEnumerable<string> files)
        {
            if (_files == null) return;
            try
            {
                foreach (var file in files)
                {
                    var hashAlgorithm = new Sha256HashAlgorithm();
                    var hash = hashAlgorithm.ComputeHash(file);
                    var entity = await Handle(file, hash);
                    _configCache.TryAdd(hash, entity);
                }
                _configCache.Build();
            }
            catch (Exception ex)
            {
                throw new Exception($"'Cache' error :{ex.Message} .", ex.InnerException);
            }
        }

        /// <summary>
        /// Process file content.
        /// </summary>
        /// <param name="file">file path</param>
        /// <param name="hash">hash</param>
        /// <returns></returns>
        private async Task<ConfigEntity> Handle(string file, string hash)
        {
            var entity = new ConfigEntity();
            entity.Path = file;
            entity.Name = Path.GetFileName(file);
            entity.OldPath = Path.Combine(_appPath, entity.Name);
            entity.Hash = hash;
            entity.Handle = ToEnum(file);
            entity.Content = await InitHandle<object>(entity.Handle).Read(file);
            return entity;
        }

        /// <summary>
        /// Initialize the corresponding file processing object.
        /// </summary>
        /// <typeparam name="TEntity">file entity</typeparam>
        /// <param name="handleEnum">handle enum</param>
        /// <returns>handle</returns>
        private IHandle<TEntity> InitHandle<TEntity>(HandleEnum handleEnum) where TEntity : class
        {
            IHandle<TEntity> handle = null;
            switch (handleEnum)
            {
                case HandleEnum.DB:
                    handle = new DBHandle<TEntity>();
                    break;

                case HandleEnum.INI:
                    handle = new IniHandle<TEntity>();
                    break;

                case HandleEnum.JSON:
                    handle = new JsonHandle<TEntity>();
                    break;

                case HandleEnum.XML:
                    handle = new XmlHandle<TEntity>();
                    break;
            }
            return handle;
        }

        /// <summary>
        /// Convert enumeration value according to file type.
        /// </summary>
        /// <param name="file">file path</param>
        /// <returns>handle enum</returns>
        private HandleEnum ToEnum(string file)
        {
            var fileExtension = Path.GetExtension(file);
            var handleEnum = HandleEnum.NONE;
            switch (fileExtension)
            {
                case ".db":
                    handleEnum = HandleEnum.DB;
                    break;

                case ".ini":
                    handleEnum = HandleEnum.INI;
                    break;

                case ".json":
                    handleEnum = HandleEnum.JSON;
                    break;

                case ".xml":
                    handleEnum = HandleEnum.XML;
                    break;
            }
            return handleEnum;
        }

        #endregion Private Methods
    }
}