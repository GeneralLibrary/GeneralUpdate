using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Provides centralized mapping utilities for converting between configuration objects.
    /// </summary>
    public static class ConfigurationMapper
    {
        /// <summary>
        /// Maps internal runtime configuration (GlobalConfigInfo) to process transfer parameters (ProcessInfo).
        /// This method consolidates the complex parameter passing logic previously scattered in bootstrap code.
        /// </summary>
        /// <param name="source">The internal configuration object containing all runtime state.</param>
        /// <param name="updateVersions">List of version information objects from the update server response.</param>
        /// <param name="blackFileFormats">List of blacklisted file formats from the BlackListManager.</param>
        /// <param name="blackFiles">List of blacklisted files from the BlackListManager.</param>
        /// <param name="skipDirectories">List of directories to skip from the BlackListManager.</param>
        /// <returns>A ProcessInfo object ready for serialization and inter-process communication.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
        public static ProcessInfo MapToProcessInfo(
            GlobalConfigInfo source,
            List<VersionInfo> updateVersions,
            List<string> blackFileFormats,
            List<string> blackFiles,
            List<string> skipDirectories)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source), "GlobalConfigInfo source cannot be null");

            return new ProcessInfo(
                appName: source.MainAppName,
                installPath: source.InstallPath,
                currentVersion: source.ClientVersion,
                lastVersion: source.LastVersion,
                updateLogUrl: source.UpdateLogUrl,
                compressEncoding: source.Encoding,
                compressFormat: source.Format,
                downloadTimeOut: source.DownloadTimeOut,
                appSecretKey: source.AppSecretKey,
                updateVersions: updateVersions,
                reportUrl: source.ReportUrl,
                backupDirectory: source.BackupDirectory,
                bowl: source.Bowl,
                scheme: source.Scheme,
                token: source.Token,
                script: source.Script,
                driverDirectory: source.DriverDirectory,
                blackFileFormats: blackFileFormats,
                blackFiles: blackFiles,
                skipDirectories: skipDirectories
            );
        }
    }
}
