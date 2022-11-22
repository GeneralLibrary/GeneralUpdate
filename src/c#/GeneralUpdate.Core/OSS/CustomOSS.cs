using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public class CustomOSS : IOSS
    {
        private string tmepPath = "";
        private VersionInfo versionInfo;
        private string _url;

        public Task<string> Download()
        {
            var manager = new DownloadManager<VersionInfo>(tmepPath, ".zip", 60);
            manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted; ;
            manager.MutiDownloadCompleted += OnMutiDownloadCompleted; ;
            manager.MutiDownloadError += OnMutiDownloadError; ;
            manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged; ;
            manager.MutiDownloadStatistics += OnMutiDownloadStatistics;
            manager.Add(new DownloadTask<VersionInfo>(manager,versionInfo));
            manager.LaunchTaskAsync();
            return Task.FromResult("");
        }

        private void OnMutiDownloadStatistics(object sender, Bootstrap.MutiDownloadStatisticsEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnMutiDownloadProgressChanged(object csender, Bootstrap.MutiDownloadProgressChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnMutiDownloadError(object sender, Bootstrap.MutiDownloadErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnMutiDownloadCompleted(object sender, Bootstrap.MutiDownloadCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnMutiAllDownloadCompleted(object sender, Bootstrap.MutiAllDownloadCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void SetParmeter(string url)
        {
            _url = url;
        }
    }
}
