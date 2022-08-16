using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class ProcessInfo : Entity
    {
        public ProcessInfo(int appType, string appName, string mainAppName, string installPath, string clientVersion, string lastVersion, string updateLogUrl, bool isUpdate, string updateUrl, string validateUrl, string mainUpdateUrl, string mainValidateUrl, int compressEncoding, string compressFormat, int downloadTimeOut, string appSecretKey, List<VersionInfo> updateVersions)
        {
            AppType = appType;
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            MainAppName = mainAppName ?? throw new ArgumentNullException(nameof(mainAppName));
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            ClientVersion = clientVersion ?? throw new ArgumentNullException(nameof(clientVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            UpdateLogUrl = updateLogUrl ?? throw new ArgumentNullException(nameof(updateLogUrl));
            IsUpdate = isUpdate;
            UpdateUrl = updateUrl ?? throw new ArgumentNullException(nameof(updateUrl));
            ValidateUrl = validateUrl ?? throw new ArgumentNullException(nameof(validateUrl));
            MainUpdateUrl = mainUpdateUrl ?? throw new ArgumentNullException(nameof(mainUpdateUrl));
            MainValidateUrl = mainValidateUrl ?? throw new ArgumentNullException(nameof(mainValidateUrl));
            CompressEncoding = compressEncoding;
            CompressFormat = compressFormat ?? throw new ArgumentNullException(nameof(compressFormat));
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            UpdateVersions = updateVersions ?? throw new ArgumentNullException(nameof(updateVersions));
        }

        /// <summary>
        /// 1:MainApp 2:UpdateApp
        /// </summary>
        public int AppType { get; set; }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        public string AppName { get; set; }

        public string MainAppName { get; set; }

        /// <summary>
        /// Installation directory (the path where the update package is decompressed).
        /// </summary>
        public string InstallPath { get; set; }

        public string ClientVersion { get; set; }

        public string LastVersion { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// Whether to update.
        /// </summary>
        public bool IsUpdate { get; set; }

        /// <summary>
        /// Update check api address.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        /// Validate update url.
        /// </summary>
        public string ValidateUrl { get; set; }

        public string MainUpdateUrl { get; set; }

        public string MainValidateUrl { get; set; }

        public int CompressEncoding { get; set; }

        public string CompressFormat { get; set; }

        public int DownloadTimeOut { get; set; }

        /// <summary>
        /// application key
        /// </summary>
        public string AppSecretKey { get; set; }

        /// <summary>
        /// One or more version update information.
        /// </summary>
        public List<VersionInfo> UpdateVersions { get; set; }
    }
}
