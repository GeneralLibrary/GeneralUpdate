using System;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class Configinfo : Entity
    {
        public Configinfo()
        { }

        public Configinfo(int appType, string appName, string appSecretKey, string clientVersion, string updateUrl, string updateLogUrl, string installPath, string mainUpdateUrl, string mainAppName)
        {
            AppType = appType;
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            ClientVersion = clientVersion ?? throw new ArgumentNullException(nameof(clientVersion));
            UpdateUrl = updateUrl ?? throw new ArgumentNullException(nameof(updateUrl));
            UpdateLogUrl = updateLogUrl ?? throw new ArgumentNullException(nameof(updateLogUrl));
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            MainUpdateUrl = mainUpdateUrl ?? throw new ArgumentNullException(nameof(mainUpdateUrl));
            MainAppName = mainAppName ?? throw new ArgumentNullException(nameof(mainAppName));
        }

        /// <summary>
        /// 1:ClientApp 2:UpdateApp
        /// </summary>
        public int AppType { get; set; }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// application key
        /// </summary>
        public string AppSecretKey { get; set; }

        /// <summary>
        /// Client current version.
        /// </summary>
        public string ClientVersion { get; set; }

        /// <summary>
        /// Update check api address.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// installation path (for update file logic).
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Update check api address.
        /// </summary>
        public string MainUpdateUrl { get; set; }

        public string MainAppName { get; set; }
    }
}