using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys
{
    public sealed class OSSStrategy : AbstractStrategy
    {
        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const string _format = ".zip";
        private ParamsOSS _parameter;

        public override void Create<T>(T parameter)
        {
            _parameter = parameter as ParamsOSS;
        }

        public override async Task ExcuteTaskAsync()
        {
            try
            {
                //1.Download the JSON version configuration file.
                var jsonUrl = $"{_parameter.Url}/{_parameter.VersionFileName}";
                var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
                //await DownloadFileAsync(jsonUrl, jsonPath, (e, s) => EventManager.Instance.Dispatch<DownloadEventHandler>(this, new OSSDownloadArgs(e, s)));
                if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                //2.Parse the JSON version configuration file content.
                byte[] jsonBytes = File.ReadAllBytes(jsonPath);
                string json = Encoding.Default.GetString(jsonBytes);
                var versions = JsonConvert.DeserializeObject<List<VersionConfigDO>>(json);
                if (versions == null) throw new NullReferenceException(nameof(versions));

                //3.Compare with the latest version.
                versions = versions.OrderBy(v => v.PubTime).ToList();
                var currentVersion = new Version(_parameter.CurrentVersion);
                var lastVersion = new Version(versions[0].Version);
                if (currentVersion.Equals(lastVersion)) return;

                foreach (var version in versions)
                {
                    string filePath = _appPath;
                    //await DownloadFileAsync(version.Url, _appPath, (e, s) => EventManager.Instance.Dispatch<DownloadEventHandler>(this, new OSSDownloadArgs(e, s)));
                    var factory = new GeneralZipFactory();
                    factory.CreatefOperate(Zip.Factory.OperationType.GZip, version.Name, _appPath, _appPath, true, Encoding.UTF8);
                    //factory.Completed += (s, e) => EventManager.Instance.Dispatch<UnZipCompletedEventHandler>(this, e);
                    //factory.UnZipProgress += (s, e) => EventManager.Instance.Dispatch<UnZipProgressEventHandler>(this, e);
                    factory.UnZip();
                }

                //5.Launch the main application.
                string appPath = Path.Combine(_appPath, _parameter.AppName);
                Process.Start(appPath);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.StackTrace);
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        //private void Download(List<VersionInfo> versions) 
        //{
        //    var manager = new DownloadManager<VersionInfo>(_appPath, _format, 60);
        //    manager.MutiAllDownloadCompleted += (s,e)=> EventManager.Instance.Dispatch<MutiAllDownloadCompletedEventHandler>(s, new MutiAllDownloadCompletedEventArgs(e.IsAllDownloadCompleted,e.FailedVersions));
        //    manager.MutiDownloadCompleted += (s,e) => EventManager.Instance.Dispatch<MutiAsyncCompletedEventHandler>(s, new MutiDownloadCompletedEventArgs(e.Version,e.Error,e.Cancelled,e.UserState));
        //    manager.MutiDownloadError += (s,e) => EventManager.Instance.Dispatch<MutiDownloadErrorEventHandler>(s, new MutiDownloadErrorEventArgs(e.Exception,e.Version));
        //    manager.MutiDownloadProgressChanged += (s,e) => EventManager.Instance.Dispatch<MutiDownloadProgressChangedEventHandler>(s, new MutiDownloadProgressChangedEventArgs(s,e.Type,e.Message,e.BytesReceived,e.TotalBytesToReceive,e.ProgressPercentage,e.UserState));
        //    manager.MutiDownloadStatistics += (s,e)=> EventManager.Instance.Dispatch<MutiDownloadStatisticsEventHandler>(s,new MutiDownloadStatisticsEventArgs(e.Version,e.Remaining,e.Speed));
        //    versions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, v)));
        //    manager.LaunchTaskAsync();
        //}
    }
}
