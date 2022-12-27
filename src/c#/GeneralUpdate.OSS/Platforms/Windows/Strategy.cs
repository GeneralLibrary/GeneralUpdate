using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Pipelines;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.Middleware;
using GeneralUpdate.Core.Utils;
using GeneralUpdate.OSS.Strategys;
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

        public override void Create(params string[] arguments)
        {
            _url = arguments[0];
            _app = arguments[1];
            _versionFileName = arguments[2];
            _currentVersion = arguments[3];
        }

        public override async Task Excute()
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
            versions = versions.OrderBy(v=>v.PubTime).ToList();
            var currentVersion = new Version(_currentVersion);
            var lastVersion = new Version(versions[0].Version);
            if (currentVersion.Equals(lastVersion)) return;

            //4.Download the packet file.
            foreach (var version in versions)
            {
                var file = $"{_appPath}/{version.Name}{version.Format}";
                await DownloadFileAsync(version.Url, file, null);
                //var patchPath = GeneralUpdate.Core.Utils.FileUtil.GetTempDirectory(PATCHS);
                //var zipFilePath = $"{Packet.TempPath}{version.Name}{Packet.Format}";
                //var pipelineBuilder = new PipelineBuilder<BaseContext>(new BaseContext(ProgressEventAction, ExceptionEventAction, version, zipFilePath, patchPath, Packet.InstallPath, Packet.Format, Packet.Encoding)).
                //    UseMiddleware<MD5Middleware>().
                //    UseMiddleware<ZipMiddleware>().
                //    UseMiddleware<PatchMiddleware>();
                //await pipelineBuilder.Launch();
            }

            //5.Launch the main application.
            string appPath = Path.Combine(_appPath, _app);
            Process.Start(appPath);
            Process.GetCurrentProcess().Kill();
        }
    }
}