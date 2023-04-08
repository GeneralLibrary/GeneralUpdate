using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Domain.PO;
using GeneralUpdate.Core.Domain.PO.Assembler;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Utils;
using GeneralUpdate.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys
{
    public sealed class OSSStrategy : AbstractStrategy
    {
        #region Private Members

        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const string _format = ".zip";
        private const int _timeOut = 60;
        private ParamsOSS _parameter;
        private Encoding _encoding;

        #endregion

        #region Public Methods

        public override void Create<T>(T parameter, Encoding encoding)
        {
            _parameter = parameter as ParamsOSS;
            _encoding = encoding;
        }

        public override async Task ExecuteTaskAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    //1.Download the JSON version configuration file.
                    var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
                    if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                    //2.Parse the JSON version configuration file content.
                    var versions = FileUtil.ReadJsonFile<List<VersionPO>>(jsonPath);
                    if (versions == null) throw new NullReferenceException(nameof(versions));

                    //3.Download version by version according to the version of the configuration file.
                    var versions1 = VersionAssembler.ToDataObjects(versions);
                    DownloadVersions(versions1);
                    UnZip(versions1);
                    //4.Launch the main application.
                    LaunchApp();
                }
                catch (Exception ex)
                {
                    EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(ex));
                }
                finally
                {
                    Process.GetCurrentProcess().Kill();
                }
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Download all updated versions version by version.
        /// </summary>
        /// <param name="versions">The collection of version information to be updated as described in the configuration file.</param>
        private void DownloadVersions(List<VersionInfo> versions)
        {
            var manager = new DownloadManager<VersionInfo>(_appPath, _format, _timeOut);
            manager.MultiAllDownloadCompleted += (s, e) => EventManager.Instance.Dispatch<Action<object, MultiAllDownloadCompletedEventArgs>>(this, e);
            manager.MultiDownloadCompleted += (s, e) => EventManager.Instance.Dispatch<Action<object, MultiDownloadCompletedEventArgs>>(this, e);
            manager.MultiDownloadError += (s, e) => EventManager.Instance.Dispatch<Action<object, MultiDownloadErrorEventArgs>>(this, e);
            manager.MultiDownloadProgressChanged += (s, e) => EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, e);
            manager.MultiDownloadStatistics += (s, e) => EventManager.Instance.Dispatch<Action<object, MultiDownloadStatisticsEventArgs>>(this, e);
            versions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, v)));
            manager.LaunchTaskAsync();
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

        /// <summary>
        /// Remove update redundant files.
        /// </summary>
        /// <returns></returns>
        private bool Clear()
        {
            try
            {
                //if (System.IO.File.Exists(Packet.TempPath)) System.IO.File.Delete(Packet.TempPath);
                //var dirPath = Path.GetDirectoryName(Packet.TempPath);
                //if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
                return true;
            }
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return false;
            }
        }

        private bool UnZip(List<VersionInfo> versions)
        {
            try
            {
                bool isCompleted = true;
                foreach (VersionInfo version in versions) 
                {
                    var zipFilePath = Path.Combine(_appPath,version.Name);

                    //var zipFactory = new GeneralZipFactory();
                    //zipFactory.UnZipProgress += (sender, e) =>
                    //EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(version, ProgressType.Updatefile, "Updating file..."));
                    //zipFactory.Completed += (sender, e) => isCompleted = e.IsCompleted;
                    //zipFactory.CreateOperate(_format, version.Name, zipFilePath, _appPath, false, _encoding)
                    //    .UnZip();
                }
                return isCompleted;
            }
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return false;
            }
        }

        #endregion
    }
}