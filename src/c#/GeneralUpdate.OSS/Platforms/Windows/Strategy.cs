using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Domain.DO.Assembler;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Pipelines;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.Middleware;
using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.OSSStrategys;
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

        Action<object, MutiDownloadStatisticsEventArgs> MutiDownloadStatisticsAction { get; set; }

        Action<object, MutiDownloadProgressChangedEventArgs> MutiDownloadProgressChangedAction { get; set; }

        Action<object, MutiDownloadCompletedEventArgs> MutiDownloadCompletedAction { get; set; }

        Action<object, MutiAllDownloadCompletedEventArgs> MutiAllDownloadCompletedAction { get; set; }

        Action<object, MutiDownloadErrorEventArgs> MutiDownloadErrorAction { get; set; }

        Action<object, ExceptionEventArgs> ExceptionEventAction { get; set; }

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

                //4.Download the packet file.
                var manager = new DownloadManager<VersionInfo>(_appPath, ".zip", 60);
                manager.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                manager.MutiDownloadCompleted += OnMutiDownloadCompleted;
                manager.MutiDownloadError += OnMutiDownloadError;
                manager.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                manager.MutiDownloadStatistics += OnMutiDownloadStatistics;
                versions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, VersionAssembler.ToDataObject(v))));
                manager.LaunchTaskAsync();

                foreach (var version in versions) 
                {
                    var pipelineBuilder = new PipelineBuilder<BaseContext>(InitContext()).
                        UseMiddleware<MD5Middleware>().
                        UseMiddleware<ZipMiddleware>();
                    await pipelineBuilder.Launch();
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

        private void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        {
            _parameter?.MutiDownloadStatisticsAction(sender, e);
        }

        private void OnMutiDownloadProgressChanged(object sender, MutiDownloadProgressChangedEventArgs e)
        {
            _parameter?.MutiDownloadProgressChangedAction(sender, e);
        }

        private void OnMutiDownloadCompleted(object sender, MutiDownloadCompletedEventArgs e)
        {
            _parameter?.MutiDownloadCompletedAction(sender, e);
        }

        private void OnMutiAllDownloadCompleted(object sender, MutiAllDownloadCompletedEventArgs e)
        {
            _parameter?.MutiAllDownloadCompletedAction(sender, e);
        }

        private void OnMutiDownloadError(object sender, MutiDownloadErrorEventArgs e)
        {
            _parameter?.MutiDownloadErrorAction(sender, e);
        }

        private BaseContext InitContext() 
            => new BaseContext( null, _appPath, _appPath, _appPath, ".zip", Encoding.Default, _parameter.MutiDownloadProgressChangedAction, _parameter.ExceptionEventAction);
    }
}