using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Events;
using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.Events;
using GeneralUpdate.Zip;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using static GeneralUpdate.Core.OSS.GeneralUpdateOSS;

namespace GeneralUpdate.OSS.OSSStrategys
{
    public class OSSStrategy : AbstractStrategy
    {
        private readonly string _appPath = FileSystem.AppDataDirectory;
        private string _url, _app, _versionFileName, _currentVersion;
        private ParamsOSS _parameter;

        public override void Create<T>(T parameter)
        {
            _parameter = parameter as ParamsOSS;
        }

        [SupportedOSPlatform("ios14.0")]
        public override async Task Excute()
        {
            try
            {
                //1.Download the JSON version configuration file.
                var jsonUrl = $"{_url}/{_versionFileName}";
                var jsonPath = Path.Combine(_appPath, _versionFileName);
                await DownloadFileAsync(jsonUrl, jsonPath, null);
                if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                //2.Parse the JSON version configuration file content.
                byte[] jsonBytes = File.ReadAllBytes(jsonPath);
                string json = Encoding.Default.GetString(jsonBytes);
                var versions = JsonConvert.DeserializeObject<List<VersionConfigDO>>(json);
                if (versions == null) throw new NullReferenceException(nameof(versions));

                //3.Compare with the latest version.
                versions = versions.OrderBy(v => v.PubTime).ToList();
                var currentVersion = new Version(_currentVersion);
                var lastVersion = new Version(versions[0].Version);
                if (currentVersion.Equals(lastVersion)) return;

                foreach (var version in versions)
                {
                    string filePath = _appPath;
                    await DownloadFileAsync(version.Url, _appPath, (e, s) => EventManager.Instance.Dispatch<DownloadEventHandler>(this, new OSSDownloadArgs(e, s)));
                    var factory = new GeneralZipFactory();
                    factory.CreatefOperate(Zip.Factory.OperationType.GZip, version.Name, _appPath, _appPath, true, Encoding.UTF8);
                    factory.Completed += (s, e) => EventManager.Instance.Dispatch<UnZipCompletedEventHandler>(this, e);
                    factory.UnZipProgress += (s, e) => EventManager.Instance.Dispatch<UnZipProgressEventHandler>(this, e);
                    factory.UnZip();
                }

                //5.Launch the main application.
                string appPath = Path.Combine(_appPath, _app);
                Process.Start(appPath);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
