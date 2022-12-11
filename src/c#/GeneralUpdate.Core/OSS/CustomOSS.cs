using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Download;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GeneralUpdate.Core.OSS
{
    public class CustomOSS : IOSS
    {
        private string _tempPath, _url, _filename,_format;
        private int _downloadTimeOut;

        public void SetParameter(string url,string fileName , string format,int timeOut) 
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
            LaunchDownload(versions, () => 
            {
                var jsonFile = Path.Combine(_tempPath, _filename);
                if (!File.Exists(jsonFile)) return;
                var json = File.ReadAllText(jsonFile);
                if (string.IsNullOrWhiteSpace(json)) return;
                //Get all update packages.
                versions = JsonConvert.DeserializeObject<List<VersionConfigDO>>(json);
                LaunchDownload(versions, () => { });
            });
        }

        private void LaunchDownload(List<VersionConfigDO> versions, Action completedCallback) 
        {
            var manager = new DownloadManager<VersionConfigDO>(_tempPath, _format, _downloadTimeOut);
            manager.MutiAllDownloadCompleted += (o,e) => completedCallback.Invoke();
            manager.MutiDownloadError += OnMutiDownloadError;
            versions.ForEach((v) => manager.Add(new DownloadTask<VersionConfigDO>(manager, v)));
            manager.LaunchTaskAsync();
        }

        private void OnMutiDownloadError(object sender, Bootstrap.MutiDownloadErrorEventArgs e)
        {
        }
    }
}
