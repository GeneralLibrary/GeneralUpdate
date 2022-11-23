using GeneralUpdate.Core.Domain.DO;

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

        public void Download()
        {
            DownloadFileRange(_name,_url, _targetPath);
        }
    }
}
