using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.ClientCore;

public sealed class GeneralClientOSS
{
    private GeneralClientOSS()
    {
    }

    /// <summary>
    ///     Starting an OSS update for windows,Linux,mac platform.
    /// </summary>
    /// <param name="configInfo"></param>
    public static async Task Start(ParamsOSS configParams, string upgradeAppName = "GeneralUpdate.Upgrade")
    {
        await Task.Run(() =>
        {
            try
            {
                var basePath = Thread.GetDomain().BaseDirectory;
                //Download the version information file from OSS to be updated.(JSON)
                var versionsFilePath = Path.Combine(basePath, configParams.VersionFileName);
                DownloadFile(configParams.Url + "/" + configParams.VersionFileName, versionsFilePath);
                if (!File.Exists(versionsFilePath)) return;
                var versions = GeneralFileManager.GetJson<List<VersionPO>>(versionsFilePath);
                if (versions == null || versions.Count == 0) return;
                versions = versions.OrderByDescending(x => x.PubTime).ToList();
                var newVersion = versions.First();
                //Determine whether the current client version needs to be upgraded.
                if (!IsUpgrade(configParams.CurrentVersion, newVersion.Version)) return;
                var appPath = Path.Combine(basePath, $"{upgradeAppName}.exe");
                if (!File.Exists(appPath)) throw new Exception($"The application does not exist {upgradeAppName} !");
                //If you confirm that an update is required, start the upgrade application.
                var json = JsonSerializer.Serialize(configParams);
                //TODO: set environment variable
                Process.Start(appPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"GeneralClientOSS update exception ! {ex.Message}", ex.InnerException);
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        });
    }

    /// <summary>
    ///     Determine whether the current client version needs to be upgraded.
    /// </summary>
    /// <param name="clientVersion"></param>
    /// <param name="serverVersion"></param>
    /// <returns>true: Upgrade required , false: No upgrade is required</returns>
    private static bool IsUpgrade(string clientVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion) || string.IsNullOrWhiteSpace(serverVersion)) return false;
        Version currentClientVersion = null;
        Version currentServerVersion = null;
        var isParseClientVersion = Version.TryParse(clientVersion, out currentClientVersion);
        var isParseServerVersion = Version.TryParse(serverVersion, out currentServerVersion);
        if (!isParseClientVersion || !isParseServerVersion) return false;
        if (currentClientVersion < currentServerVersion) return true;
        return false;
    }

    private static void DownloadFile(string url, string path)
    {
        using var webClient = new WebClient();
        webClient.DownloadFile(new Uri(url), path);
    }

    public static void AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
     => AddListener(callbackAction);

    private static void AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
    {
        Contract.Requires(callbackAction != null);
        EventManager.Instance.AddListener(callbackAction);
    }
}