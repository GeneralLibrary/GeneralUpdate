using GeneralUpdate.Core.Download;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public class CustomOSS : IOSS
    {
        private string _tempPath, _url;
        private string _filename;

        public void SetParmeter(string url)
        {
            _url = url;
            _tempPath = Thread.GetDomain().BaseDirectory;
        }

        public async Task<string> Download()
        {
            await Task.Run(() => 
            {
                //var download = new DownloadOSS(_url, _filename, _tempPath);
                //download.Download();
            });
            return await Task.FromResult(Path.Combine(_tempPath,_filename));
        }
    }
}
