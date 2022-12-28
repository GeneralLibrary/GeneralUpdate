using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class Packet : Entity
    {
        public Packet()
        { }

        public Packet(string mainUpdateUrl, int appType, string updateUrl, string appName, string mainAppName, string format, bool isUpdate, string updateLogUrl, Encoding encoding, int downloadTimeOut, string appSecretKey, string tempPath)
        {
            if (!IsURL(mainUpdateUrl)) throw new Exception($"Illegal url {nameof(mainUpdateUrl)}");
            MainUpdateUrl = mainUpdateUrl ?? throw new ArgumentNullException(nameof(MainUpdateUrl));
            if (!IsURL(updateUrl)) throw new Exception($"Illegal url {nameof(UpdateUrl)}");
            UpdateUrl = updateUrl ?? throw new ArgumentNullException(nameof(updateUrl));
            UpdateLogUrl = updateLogUrl ?? throw new ArgumentNullException(nameof(updateLogUrl));
            AppType = appType;
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            MainAppName = mainAppName ?? throw new ArgumentNullException(nameof(mainAppName));
            Format = format ?? throw new ArgumentNullException(nameof(format));
            IsUpgradeUpdate = isUpdate;
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            TempPath = tempPath ?? throw new ArgumentNullException(nameof(tempPath));
        }

        /// <summary>
        /// Update check api address.
        /// </summary>
        public string MainUpdateUrl { get; set; }

        /// <summary>
        /// 1:ClientApp 2:UpdateApp
        /// </summary>
        public int AppType { get; set; }

        /// <summary>
        /// Update check api address.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        public string AppName { get; set; }

        public string MainAppName { get; set; }

        /// <summary>
        /// Update package file format(Defult format is Zip).
        /// </summary>
        public string Format { get; set; }

        public bool IsUpgradeUpdate { get; set; }

        public bool IsMainUpdate { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// Version information that needs to be updated.
        /// </summary>
        public List<VersionInfo> UpdateVersions { get; set; }

        public Encoding Encoding { get; set; }

        public int DownloadTimeOut { get; set; }

        /// <summary>
        /// application key
        /// </summary>
        public string AppSecretKey { get; set; }

        /// <summary>
        /// Client current version.
        /// </summary>
        public string ClientVersion { get; set; }

        /// <summary>
        /// The latest version.
        /// </summary>
        public string LastVersion { get; set; }

        /// <summary>
        /// installation path (for update file logic).
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Download file temporary storage path (for update file logic).
        /// </summary>
        public string TempPath { get; set; }

        public string ProcessBase64 { get; set; }

        public string Platform { get; set; }
    }
}