using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.OSSStrategys;
using GeneralUpdate.Zip;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace GeneralUpdate.OSS
{
    // All the code in this file is only included on Windows.
    public class Strategy : AbstractStrategy
    {
        private readonly string _appPath = FileSystem.AppDataDirectory;
        private string _url, _app, _versionFileName, _currentVersion;
        private ParamsWindows _parameter;

        public override void Create<T>(T parameter)
        {
            _parameter = parameter as ParamsWindows;
        }

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
                    await DownloadFileAsync(version.Url, _appPath, (e,s) => { });
                    var factory = new GeneralZipFactory();
                    factory.CreatefOperate(Zip.Factory.OperationType.GZip,"","","",true,Encoding.UTF8);
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