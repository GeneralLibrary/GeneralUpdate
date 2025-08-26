using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.Compress;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Core.Strategys
{
    public class OSSStrategy
    {
        #region Private Members

        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const int TimeOut = 60;
        private GlobalConfigInfoOSS? _parameter;

        #endregion Private Members

        #region Public Methods

        public void Create(GlobalConfigInfoOSS parameter)
            => _parameter = parameter;

        public async Task ExecuteAsync()
        {
            try
            {
                //1.Download the JSON version configuration file.
                var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
                if (!File.Exists(jsonPath)) 
                    throw new FileNotFoundException(jsonPath);

                //2.Parse the JSON version configuration file content.
                var versions = StorageManager.GetJson<List<VersionOSS>>(jsonPath, VersionOSSJsonContext.Default.ListVersionOSS);
                if (versions == null) 
                    throw new NullReferenceException(nameof(versions));

                versions = versions.OrderBy(v => v.PubTime).ToList();
                //3.Download version by version according to the version of the configuration file.
                await DownloadVersions(versions);
                Decompress(versions);
                    
                //4.Launch the main application.
                LaunchApp();
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("The ExecuteAsync method in the OSSStrategy class throws an exception.", ex);
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Download all updated versions version by version.
        /// </summary>
        /// <param name="versions">The collection of version information to be updated as described in the configuration file.</param>
        private async Task DownloadVersions(List<VersionOSS> versions)
        {
            var manager = new DownloadManager(_appPath, Format.ZIP, TimeOut);
            foreach (var versionInfo in versions)
            {
                var version = new VersionInfo
                {
                    Name = versionInfo.PacketName,
                    Version = versionInfo.Version,
                    Url = versionInfo.Url,
                    Format = Format.ZIP,
                    Hash = versionInfo.Hash
                };
                manager.Add(new DownloadTask(manager, version));
            }

            await manager.LaunchTasksAsync();
        }

        /// <summary>
        /// Start the main application when the update is complete.
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        private void LaunchApp()
        {
            var appPath = Path.Combine(_appPath, _parameter.AppName);
            if (!File.Exists(appPath)) throw new FileNotFoundException($"{nameof(appPath)} , The application is not accessible !");
            Process.Start(appPath);
        }

        private void Decompress(List<VersionOSS> versions)
        {
            var encoding = Encoding.GetEncoding(_parameter.Encoding);
            foreach (var version in versions)
            {
                var zipFilePath = Path.Combine(_appPath, $"{version.PacketName}{Format.ZIP}");
                CompressProvider.Decompress(Format.ZIP, zipFilePath, _appPath, encoding);

                if (!File.Exists(zipFilePath)) continue;
                File.SetAttributes(zipFilePath, FileAttributes.Normal);
                File.Delete(zipFilePath);
            }
        }

        #endregion Private Methods
    }
}