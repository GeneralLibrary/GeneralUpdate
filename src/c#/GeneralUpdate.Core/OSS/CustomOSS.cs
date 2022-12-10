using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Download;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GeneralUpdate.Core.OSS
{
    public class CustomOSS //: IOSS
    {
        private string _tempPath, _url, _filename,_format;
        private int _downloadTimeOut;

        public CustomOSS(string url,string fileName , string format,int timeOut)
        {
            _url = url;
            _filename = fileName;
            _format = format;
            _downloadTimeOut = timeOut;
            _tempPath = Thread.GetDomain().BaseDirectory;
        }

        public void Update()
        {
            List<VersionConfigDO> versions = null;
            //Obtain the updated version information file (.json) .
            versions = new List<VersionConfigDO>();
            var version = new VersionConfigDO(null,_url,null,_filename,_format,null,0);
            versions.Add(version);
            LaunchDownload(versions);
            //Get all update packages.
            var jsonText =  File.ReadAllText("");
            versions = JsonConvert.DeserializeObject<List<VersionConfigDO>>(jsonText);
            LaunchDownload(versions);
        }

        private void LaunchDownload(List<VersionConfigDO> versions) 
        {
            var manager = new DownloadManager<VersionConfigDO>(_tempPath, _format, _downloadTimeOut);
            manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
            manager.MutiDownloadCompleted += OnMutiDownloadCompleted;
            manager.MutiDownloadError += OnMutiDownloadError;
            manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged; ;
            manager.MutiDownloadStatistics += OnMutiDownloadStatistics; ;
            versions.ForEach((v) => manager.Add(new DownloadTask<VersionConfigDO>(manager, v)));
            manager.LaunchTaskAsync();
        }

        private void OnMutiDownloadStatistics(object sender, Bootstrap.MutiDownloadStatisticsEventArgs e)
        {
        }

        private void OnMutiDownloadProgressChanged(object csender, Bootstrap.MutiDownloadProgressChangedEventArgs e)
        {
        }

        private void OnMutiDownloadError(object sender, Bootstrap.MutiDownloadErrorEventArgs e)
        {
        }

        private void OnMutiDownloadCompleted(object sender, Bootstrap.MutiDownloadCompletedEventArgs e)
        {
        }

        private void OnMutiAllDownloadCompleted(object sender, Bootstrap.MutiAllDownloadCompletedEventArgs e)
        {
        }
    }
}
