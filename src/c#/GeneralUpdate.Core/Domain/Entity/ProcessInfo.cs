using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.DTO.Assembler;
using GeneralUpdate.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class ProcessInfo : Entity
    {
        public ProcessInfo() { }

        public ProcessInfo(int appType, string appName, string installPath, string currentVersion, string lastVersion, string logUrl, bool isUpdate,Encoding compressEncoding, string compressFormat, int downloadTimeOut, string appSecretKey, List<VersionDTO> updateVersions)
        {
            AppType = appType;
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if(!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! { installPath }." );
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            LogUrl = logUrl ?? throw new ArgumentNullException(nameof(logUrl));
            IsUpdate = isUpdate;
            CompressEncoding = ConvertUtil.ToEncodingType(compressEncoding);
            CompressFormat = compressFormat ?? throw new ArgumentNullException(nameof(compressFormat));
            if (downloadTimeOut <= 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = VersionAssembler.ToEntitys(updateVersions);
        }

        /// <summary>
        /// 1:MainApp 2:UpdateApp
        /// </summary>
        public int AppType { get; set; }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// Installation directory (the path where the update package is decompressed).
        /// </summary>
        public string InstallPath { get; set; }

        public string CurrentVersion { get; set; }

        public string LastVersion { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        public string LogUrl { get; set; }

        /// <summary>
        /// Whether to update.
        /// </summary>
        public bool IsUpdate { get; set; }

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
