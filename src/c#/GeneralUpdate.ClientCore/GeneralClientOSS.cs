using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.ClientCore;

public sealed class GeneralClientOSS
{
    private GeneralClientOSS() { }

    /// <summary>
    /// Starting an OSS update for windows platform.
    /// </summary>
    public static async Task Start(GlobalConfigInfoOSS configGlobalConfigInfo, string upgradeAppName = "GeneralUpdate.Upgrade.exe")
    {
        await Task.Run(() =>
        {
            try
            {
                var basePath = Thread.GetDomain().BaseDirectory;
                //Download the version information file from OSS to be updated.(JSON)
                var versionsFilePath = Path.Combine(basePath, configGlobalConfigInfo.VersionFileName);
                DownloadFile(configGlobalConfigInfo.Url, versionsFilePath);
                if (!File.Exists(versionsFilePath)) return;
                var versions = StorageManager.GetJson<List<VersionOSS>>(versionsFilePath, VersionOSSJsonContext.Default.ListVersionOSS);
                if (versions == null || versions.Count == 0) return;
                versions = versions.OrderByDescending(x => x.PubTime).ToList();
                var newVersion = versions.First();
                //Determine whether the current client version needs to be upgraded.
                if (!IsUpgrade(configGlobalConfigInfo.CurrentVersion, newVersion.Version)) 
                    return;
                
                //If you confirm that an update is required, start the upgrade application.
                var appPath = Path.Combine(basePath, $"{upgradeAppName}");
                if (!File.Exists(appPath)) 
                    throw new Exception($"The application does not exist {upgradeAppName} !");
                
                var json = JsonSerializer.Serialize(configGlobalConfigInfo, GlobalConfigInfoOSSJsonContext.Default.GlobalConfigInfoOSS);
                Environment.SetEnvironmentVariable("GlobalConfigInfoOSS", json, EnvironmentVariableTarget.User);
                Process.Start(appPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw new Exception(ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        });
    }

    /// <summary>
    /// Determine whether the current client version needs to be upgraded.
    /// </summary>
    /// <param name="clientVersion"></param>
    /// <param name="serverVersion"></param>
    /// <returns>true: Upgrade required , false: No upgrade is required</returns>
    private static bool IsUpgrade(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion)) 
            return false;
        
        var isParseClientVersion = Version.TryParse(clientVersion, out var currentClientVersion);
        var isParseServerVersion = Version.TryParse(serverVersion, out var currentServerVersion);
        if (!isParseClientVersion || !isParseServerVersion) return false;
        if (currentClientVersion < currentServerVersion) return true;
        return false;
    }

    private static void DownloadFile(string url, string path)
    {
        using var webClient = new WebClient();
        webClient.DownloadFile(new Uri(url), path);
    }
}