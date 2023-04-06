using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Entity.Assembler;
using GeneralUpdate.Core.Domain.PO;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore
{
    public sealed class GeneralClientOSS
    {
        private GeneralClientOSS()
        { }

        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <param name="configInfo"></param>
        public static async Task Start(ParamsOSS configParams, string upgradeAppName = "GeneralUpdate.Upgrade")
        {
            try
            {
                string basePath = System.Threading.Thread.GetDomain().BaseDirectory;
                //Download the version information file from OSS to be updated.(JSON)
                await DownloadFileAsync(configParams.Url + "/" + configParams.VersionFileName, basePath, null);
                var versionsFilePath = Path.Combine(basePath, configParams.VersionFileName);
                if (!File.Exists(versionsFilePath)) return;
                var versions = FileUtil.ReadJsonFile<List<VersionPO>>(versionsFilePath);
                if (versions == null || versions.Count == 0) return;
                versions = versions.OrderBy(x => x.PubTime).ToList();
                var newVersion = versions.First();
                if (newVersion == null) return;
                //Determine whether the current client version needs to be upgraded.
                if (!IsUpgrade(configParams.CurrentVersion, newVersion.Version)) return;
                var appPath = Path.Combine(basePath, $"{upgradeAppName}.exe");
                if (!File.Exists(appPath)) throw new Exception($"The application does not exist {upgradeAppName} !");
                //If you confirm that an update is required, start the upgrade application.
                var processBase64 = ProcessAssembler.ToBase64(configParams);
                Process.Start(appPath, processBase64);
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                throw new Exception($"GeneralClientOSS update exception ! {ex.Message}", ex.InnerException);
            }
        }

        /// <summary>
        /// Determine whether the current client version needs to be upgraded.
        /// </summary>
        /// <param name="clientVersion"></param>
        /// <param name="serverVersion"></param>
        /// <returns>true: Upgrade required , false: No upgrade is required</returns>
        private static bool IsUpgrade(string clientVersion,string serverVersion) 
        {
            if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion)) return false;
            Version currentClientVersion = null;
            Version currentServerVersion = null;
            bool isParseClientVersion = Version.TryParse(clientVersion, out currentClientVersion);
            bool isParseServerVersion = Version.TryParse(clientVersion, out currentServerVersion);
            if(!isParseClientVersion || !isParseServerVersion) return false;
            if (currentClientVersion <= currentServerVersion) return false;
            return true;
        }

        /// <summary>
        /// download file.
        /// </summary>
        /// <param name="url">remote service address</param>
        /// <param name="filePath">download file path.</param>
        /// <param name="action">progress report.</param>
        /// <returns></returns>
        private static async Task DownloadFileAsync(string url, string filePath, Action<long, long> action)
        {
            var request = new HttpRequestMessage(new HttpMethod("GET"), url);
            var client = new HttpClient();
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var totalLength = response.Content.Headers.ContentLength;
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
                        if (totalLength == null) continue;
                        if (action != null) action(readLength, totalLength.Value);
                        fileStream.Write(buffer, 0, length);
                    }
                }
            }
        }
    }
}