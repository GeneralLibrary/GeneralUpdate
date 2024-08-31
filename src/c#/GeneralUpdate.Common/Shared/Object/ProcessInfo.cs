using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeneralUpdate.Common.Shared.Object
{
    public class ProcessInfo : Entity
    {
        public ProcessInfo()
        { }

        public ProcessInfo(string appName, string installPath, string currentVersion, string lastVersion, string logUrl, Encoding compressEncoding, string compressFormat, int downloadTimeOut, string appSecretKey, List<VersionDTO> updateVersions)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if (!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! {installPath}.");
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            LogUrl = logUrl;
            compressEncoding = compressEncoding ?? Encoding.Default;
            CompressEncoding = ToEncodingType(compressEncoding);
            CompressFormat = compressFormat;
            if (downloadTimeOut < 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = VersionAssembler.ToEntitys(updateVersions);
        }

        private int ToEncodingType(Encoding encoding)
        {
            int type = -1;
            if (encoding == Encoding.UTF8)
            {
                type = 1;
            }
            else if (encoding == Encoding.UTF7)
            {
                type = 2;
            }
            else if (encoding == Encoding.UTF32)
            {
                type = 3;
            }
            else if (encoding == Encoding.Unicode)
            {
                type = 4;
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                type = 5;
            }
            else if (encoding == Encoding.ASCII)
            {
                type = 6;
            }
            else if (encoding == Encoding.Default)
            {
                type = 7;
            }
            return type;
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