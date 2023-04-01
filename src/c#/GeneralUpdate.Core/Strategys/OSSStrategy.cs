using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Domain.DO.Assembler;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.MutiEventArgs;
using GeneralUpdate.Core.Events.OSSArgs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys
{
    public sealed class OSSStrategy : AbstractStrategy
    {
        #region Private Members

        private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
        private const string _format = ".zip";
        private ParamsOSS _parameter;

        #endregion

        #region Public Methods

        public override void Create<T>(T parameter)
        {
            _parameter = parameter as ParamsOSS;
        }

        public override async Task ExcuteTaskAsync()
        {
            try
            {
                //1.Download the JSON version configuration file.
                var jsonUrl = $"{_parameter.Url}/{_parameter.VersionFileName}";
                var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
                var isHasNewVersion = await DownloadFileAsync(jsonUrl, jsonPath, (readLength, totalLength)
                    => EventManager.Instance.Dispatch<Action<object, OSSDownloadArgs>>(this, new OSSDownloadArgs(readLength, totalLength)));
                if (!isHasNewVersion) return;
                if (!File.Exists(jsonPath)) throw new FileNotFoundException(jsonPath);

                //2.Parse the JSON version configuration file content.
                byte[] jsonBytes = File.ReadAllBytes(jsonPath);
                string json = Encoding.Default.GetString(jsonBytes);
                var versions = JsonConvert.DeserializeObject<List<VersionConfigDO>>(json);
                if (versions == null) throw new NullReferenceException(nameof(versions));

                //3.Compare with the latest version.
                versions = versions.OrderBy(v => v.PubTime).ToList();
                var currentVersion = new Version(_parameter.CurrentVersion);
                var lastVersion = new Version(versions[0].Version);
                if (currentVersion.Equals(lastVersion)) return;

                //4.Download version by version according to the version of the configuration file.
                DownloadVersions(VersionAssembler.ToDataObjects(versions));

                //5.Launch the main application.
                LaunchApp();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.StackTrace);
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Download all updated versions version by version.
        /// </summary>
        /// <param name="versions">The collection of version information to be updated as described in the configuration file.</param>
        private void DownloadVersions(List<VersionInfo> versions)
        {
            var manager = new DownloadManager<VersionInfo>(_appPath, _format, 60);
            manager.MutiAllDownloadCompleted += (s, e) => EventManager.Instance.Dispatch<Action<object, MutiAllDownloadCompletedEventArgs>>(this, e);
            manager.MutiDownloadCompleted += (s, e) => EventManager.Instance.Dispatch<Action<object, MutiDownloadCompletedEventArgs>>(this, e);
            manager.MutiDownloadError += (s, e) => EventManager.Instance.Dispatch<Action<object, MutiDownloadErrorEventArgs>>(this, e);
            manager.MutiDownloadProgressChanged += (s, e) => EventManager.Instance.Dispatch<Action<object, MutiDownloadProgressChangedEventArgs>>(this, e);
            manager.MutiDownloadStatistics += (s, e) => EventManager.Instance.Dispatch<Action<object, MutiDownloadStatisticsEventArgs>>(this, e);
            versions.ForEach((v) => manager.Add(new DownloadTask<VersionInfo>(manager, v)));
            manager.LaunchTaskAsync();
        }

        /// <summary>
        /// download file.
        /// </summary>
        /// <param name="url">remote service address</param>
        /// <param name="filePath">download file path.</param>
        /// <param name="action">progress report.</param>
        /// <returns></returns>
        private async Task<bool> DownloadFileAsync(string url, string filePath, Action<long, long> action)
        {
            var request = new HttpRequestMessage(new HttpMethod("GET"), url);
            var client = new HttpClient();
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var totalLength = response.Content.Headers.ContentLength;
            if (totalLength == 0) return false;
            var stream = await response.Content.ReadAsStreamAsync();
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (stream)
                {
                    var buffer = new byte[10240];
                    var readLength = 0;
                    int length;
                    while ((length = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        readLength += length;
                        if (action != null) action(readLength, totalLength.Value);
                        fileStream.Write(buffer, 0, length);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Start the main application when the update is complete.
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        private void LaunchApp()
        {
            string appPath = Path.Combine(_appPath, _parameter.AppName);
            if (!File.Exists(appPath)) throw new FileNotFoundException($"{nameof(appPath)} , The application is not accessible !");
            Process.Start(appPath);
        }

        #endregion
    }
}