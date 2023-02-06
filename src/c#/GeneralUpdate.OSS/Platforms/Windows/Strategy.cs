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
        private Action<long, long> _downloadAction;
        private Action<object, Zip.Events.BaseCompleteEventArgs> _unZipComplete;
        private Action<object, Zip.Events.BaseUnZipProgressEventArgs> _unZipProgress;

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
                    string filePath = _appPath;
                    await DownloadFileAsync(version.Url, _appPath, _downloadAction);
                    var factory = new GeneralZipFactory();
                    factory.CreatefOperate(Zip.Factory.OperationType.GZip, version.Name, _appPath, _appPath, true,Encoding.UTF8);
                    factory.Completed += OnCompleted;
                    factory.UnZipProgress += OnUnZipProgress;
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

        private void OnUnZipProgress(object sender, Zip.Events.BaseUnZipProgressEventArgs e)=> _unZipProgress?.Invoke(sender, e);

        private void OnCompleted(object sender, Zip.Events.BaseCompleteEventArgs e)=> _unZipComplete?.Invoke(sender, e);
    }
}