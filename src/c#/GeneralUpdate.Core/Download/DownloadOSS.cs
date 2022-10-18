using GeneralUpdate.Core.CustomAwaiter;
using GeneralUpdate.Core.Domain.DO;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Download
{
    public class DownloadOSS : AbstractTask<OSSDownloadDO>
    {
        private string _url, _name, _targetPath;

        public DownloadOSS(string url,string name,string targetPath)
        {
            _url = url;
            _name = name;
            _targetPath = targetPath;
        }

        public void Dowload()
        {
            DownloadFileRange(_name,_url, _targetPath);
        }
    }
}
