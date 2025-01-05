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
            , List<VersionInfo> updateVersions
            , string reportUrl
            , string backupDirectory
            , string bowl
            , string scheme
            , string token
            , List<string> blackFileFormats
            , List<string> blackFiles
            , List<string> skipDirectories
            , string patchPath
            , string tempPath)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            if (!Directory.Exists(installPath)) throw new ArgumentException($"{nameof(installPath)} path does not exist ! {installPath}.");
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            LastVersion = lastVersion ?? throw new ArgumentNullException(nameof(lastVersion));
            UpdateLogUrl = updateLogUrl;
            CompressEncoding = compressEncoding.WebName;
            CompressFormat = compressFormat;
            if (downloadTimeOut < 0) throw new ArgumentException("Timeout must be greater than 0 !");
            DownloadTimeOut = downloadTimeOut;
            AppSecretKey = appSecretKey ?? throw new ArgumentNullException(nameof(appSecretKey));
            if (updateVersions == null || updateVersions.Count == 0) throw new ArgumentException("Collection cannot be null or has 0 elements !");
            UpdateVersions = updateVersions;
            ReportUrl = reportUrl ?? throw new ArgumentNullException(nameof(reportUrl));
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
            Bowl = bowl;
            Scheme = scheme;
            Token = token;
            BlackFileFormats = blackFileFormats;
            BlackFiles = blackFiles;
            SkipDirectorys = skipDirectories;
            TempPath = tempPath;
            PatchPath = patchPath;
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
        public string CompressEncoding { get; set; }

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
        public List<VersionInfo> UpdateVersions { get; set; }

        /// <summary>
        /// update report web address
        /// </summary>
        [JsonPropertyName("ReportUrl")]
        public string ReportUrl { get; set; }
        
        /// <summary>
        /// Back up the current version files that have not been updated.
        /// </summary>
        [JsonPropertyName("BackupDirectory")]
        public string BackupDirectory { get; set; }
        
        [JsonPropertyName("Bowl")]
        public string Bowl { get; set; }
        
        [JsonPropertyName("Scheme")]
        public string Scheme { get; set; }
    
        [JsonPropertyName("Token")]
        public string Token { get; set; }
        
        [JsonPropertyName("BlackFileFormats")]
        public List<string> BlackFileFormats { get; set; }
        
        [JsonPropertyName("BlackFiles")]
        public List<string> BlackFiles { get; set; }

        [JsonPropertyName("SkipDirectorys")]
        public List<string> SkipDirectorys { get; set; }
        [JsonPropertyName("PatchPath")]
        public string PatchPath { get; set; }
        [JsonPropertyName("TempPath")]
        public string TempPath { get; set; }
    }
}