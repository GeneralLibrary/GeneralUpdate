using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Factory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Core.Strategys
{
    public class OSSStrategy
    {
        #region Private Members

        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const string _format = ".zip";
        private const int _timeOut = 60;
        private ParamsOSS _parameter;
        private Encoding _encoding;

        #endregion Private Members

        #region Public Methods

        public void Create(ParamsOSS parameter)
            => _parameter = parameter;

        public async Task ExecuteAsync()
        {
            try
            {
                //1.Download the JSON version configuration file.
                var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
                if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                //2.Parse the JSON version configuration file content.
                var versions = GeneralFileManager.GetJson<List<VersionPO>>(jsonPath);
                if (versions == null) 
                    throw new NullReferenceException(nameof(versions));

                versions = versions.OrderBy(v => v.PubTime).ToList();
                    
                //3.Download version by version according to the version of the configuration file.
                await DownloadVersions(versions);
                UnZip(versions);
                    
                //4.Launch the main application.
                LaunchApp();
            }
            catch (Exception ex)
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex));
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
        private async Task DownloadVersions(List<VersionPO> versions)
        {
            var manager = new DownloadManager(_appPath, _format, _timeOut);
            foreach (var versionInfo in versions)
            {
                var version = new VersionBodyDTO
                {
                    Name = versionInfo.PacketName,
                    Version = versionInfo.Version,
                    Url = versionInfo.Url,
                    Format = _format,
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

        private bool UnZip(List<VersionPO> versions)
        {
            try
            {
                bool isCompleted = true;
                foreach (var version in versions)
                {
                    var zipFilePath = Path.Combine(_appPath, $"{version.PacketName}.zip");
                    var zipFactory = new GeneralZipFactory();
                    zipFactory.UnZipProgress += (sender, e) =>
                    EventManager.Instance.Dispatch(this, new MultiDownloadProgressChangedEventArgs(version, ProgressType.Updatefile, "Updating file..."));
                    zipFactory.Completed += (sender, e) =>
                    {
                        isCompleted = e.IsCompleted;
                        if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
                    };
                    zipFactory.CreateOperate(OperationType.GZip, version.PacketName, zipFilePath, _appPath, false, _encoding);
                    zipFactory.UnZip();
                }
                return isCompleted;
            }
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception));
                return false;
            }
        }

        #endregion Private Methods
    }
}