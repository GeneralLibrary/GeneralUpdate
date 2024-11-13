using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object
{
    public class ProcessInfo
    {
        public ProcessInfo() { }

        public ProcessInfo(string appName
            , string installPath
            , string currentVersion
            , string lastVersion
            , string updateLogUrl
            , Encoding compressEncoding
            , string compressFormat
            , int downloadTimeOut
            , string appSecretKey
            , List<VersionBodyDTO> updateVersions
            , string reportUrl)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if (!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! {installPath}.");
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            UpdateLogUrl = updateLogUrl;
            CompressEncoding = ToEncodingType(compressEncoding);
            CompressFormat = compressFormat;
            if (downloadTimeOut < 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = updateVersions;
            ReportUrl = reportUrl ?? throw new ArgumentNullException(nameof(reportUrl));
        }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        [JsonPropertyName("AppName")]
        public string AppName { get; set; }

        /// <summary>
        /// Installation directory (the path where the update package is decompressed).
        /// </summary>
        [JsonPropertyName("InstallPath")]
        public string InstallPath { get; set; }

        /// <summary>
        /// Current version.
        /// </summary>
        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        /// <summary>
        /// The version of the last update.
        /// </summary>
        [JsonPropertyName("LastVersion")]
        public string LastVersion { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        [JsonPropertyName("UpdateLogUrl")]
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// The encoding type of the update package.
        /// </summary>
        [JsonPropertyName("CompressEncoding")]
        public int CompressEncoding { get; set; }

        /// <summary>
        /// The compression format of the update package.
        /// </summary>
        [JsonPropertyName("CompressFormat")]
        public string CompressFormat { get; set; }

        /// <summary>
        /// The timeout of the download.
        /// </summary>
        [JsonPropertyName("DownloadTimeOut")]
        public int DownloadTimeOut { get; set; }

        /// <summary>
        /// application key
        /// </summary>
        [JsonPropertyName("AppSecretKey")]
        public string AppSecretKey { get; set; }

        /// <summary>
        /// One or more version update information.
        /// </summary>
        [JsonPropertyName("UpdateVersions")]
        public List<VersionBodyDTO> UpdateVersions { get; set; }

        /// <summary>
        /// update report web address
        /// </summary>
        [JsonPropertyName("ReportUrl")]
        public string ReportUrl { get; set; }
        
        private static int ToEncodingType(Encoding encoding)
        {
            var type = -1;
            if (Equals(encoding, Encoding.UTF8))
            {
                type = 1;
            }
            else if (Equals(encoding, Encoding.UTF7))
            {
                type = 2;
            }
            else if (Equals(encoding, Encoding.UTF32))
            {
                type = 3;
            }
            else if (Equals(encoding, Encoding.Unicode))
            {
                type = 4;
            }
            else if (Equals(encoding, Encoding.BigEndianUnicode))
            {
                type = 5;
            }
            else if (Equals(encoding, Encoding.ASCII))
            {
                type = 6;
            }
            else if (Equals(encoding, Encoding.Default))
            {
                type = 7;
            }
            
            return type;
        }
    }
}