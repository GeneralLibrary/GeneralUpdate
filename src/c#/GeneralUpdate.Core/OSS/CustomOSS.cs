using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    //public class CustomOSS : IOSS
    //{
    //    private const string Format = ".zip";
    //    private const string TempPath = ".zip";
    //    private const int DownloadTimeOut = 60;
    //    private string _targetPath;

    //    public CustomOSS() 
    //    {
    //        _targetPath = 
    //    }

    //    public void GetVersionInfomation(string url) 
    //    {

    //    }

    //    public void GetVersions(List<string> versions) 
    //    {

    //    }

    //    private async Task<bool> Download(string url)
    //    {
    //        bool isCompleted = false;
    //        Exception exception = null;
    //        var downloadTask = new DownloadOSS(url, name);
    //        downloadTask.com
    //        await downloadTask.Launch0();
    //        //var manager = new DownloadManager<VersionInfo>(TempPath, Format, DownloadTimeOut);
    //        //manager.MutiAllDownloadCompleted += (s,e) => 
    //        //{
    //        //    isCompleted = e.IsAllDownloadCompleted;
    //        //} ;
    //        //manager.MutiDownloadError += (s, e) => 
    //        //{
    //        //    exception = e.Exception;
    //        //};
    //        //manager.Add(new DownloadTask<VersionInfo>(manager, new VersionInfo(0,null, name, null, url)));
    //        //manager.LaunchTaskAsync();
    //        if (isCompleted)
    //        {
    //            return Task.FromResult("");
    //        }
    //        else
    //        {
    //            throw exception;
    //        }
    //    }
    //}
}
