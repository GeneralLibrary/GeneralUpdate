using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Factory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Internal;

namespace GeneralUpdate.Core.Strategys
{
    public sealed class OSSStrategy : AbstractStrategy
    {
        #region Private Members

        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const string _format = ".zip";
        private const int _timeOut = 60;
        private Packet _parameter;
        private Encoding _encoding;

        #endregion Private Members

        #region Public Methods

        public override void Create(Packet parameter)
        {
            _parameter = parameter;
        }

        public override async Task ExecuteTaskAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    //1.Download the JSON version configuration file.
                    var jsonPath = Path.Combine(_appPath, "version.json");
                    if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                    //2.Parse the JSON version configuration file content.
                    var versions = GeneralFileManager.GetJson<List<VersionPO>>(jsonPath);
                    if (versions == null) throw new NullReferenceException(nameof(versions));

                    //3.Download version by version according to the version of the configuration file.
                    //var versionInfo = VersionAssembler.ToDataObjects(versions);
                    //DownloadVersions(versionInfo);
                    //UnZip(versionInfo);
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
            });
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Download all updated versions version by version.
        /// </summary>
        /// <param name="versions">The collection of version information to be updated as described in the configuration file.</param>
        private void DownloadVersions(List<VersionInfo> versions)
        {
           //TODO: download version by version
        }

        /// <summary>
        /// Start the main application when the update is complete.
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        private void LaunchApp()
        {
            string appPath = Path.Combine(_appPath, _parameter.AppName + ".exe");
            if (!File.Exists(appPath)) throw new FileNotFoundException($"{nameof(appPath)} , The application is not accessible !");
            Process.Start(appPath);
        }

        private bool UnZip(List<VersionInfo> versions)
        {
            try
            {
                bool isCompleted = true;
                foreach (VersionInfo version in versions)
                {
                    var zipFilePath = Path.Combine(_appPath, $"{version.Name}.zip");
                    var zipFactory = new GeneralZipFactory();
                    zipFactory.UnZipProgress += (sender, e) =>
                    EventManager.Instance.Dispatch(this, new MultiDownloadProgressChangedEventArgs(version, ProgressType.Updatefile, "Updating file..."));
                    zipFactory.Completed += (sender, e) =>
                    {
                        isCompleted = e.IsCompleted;
                        if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
                    };
                    zipFactory.CreateOperate(OperationType.GZip, version.Name, zipFilePath, _appPath, false, _encoding);
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