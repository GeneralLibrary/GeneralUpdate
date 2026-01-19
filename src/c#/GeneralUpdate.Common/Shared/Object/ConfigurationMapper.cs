using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Provides centralized mapping utilities for converting between configuration objects.
    /// This class ensures consistent field mapping across Configinfo, GlobalConfigInfo, and ProcessInfo,
    /// reducing the risk of missing or incorrectly mapped fields during maintenance.
    /// </summary>
    public static class ConfigurationMapper
    {
        /// <summary>
        /// Maps user-provided configuration (Configinfo) to internal runtime configuration (GlobalConfigInfo).
        /// This method performs a one-to-one field mapping for all shared configuration properties.
        /// </summary>
        /// <param name="source">The user-provided configuration object containing initial settings.</param>
        /// <param name="target">The internal configuration object to be populated. If null, a new instance is created.</param>
        /// <returns>A GlobalConfigInfo object populated with values from the source Configinfo.</returns>
        public static GlobalConfigInfo MapToGlobalConfigInfo(Configinfo source, GlobalConfigInfo target = null)
        {
            // Create new instance if both source and target are not provided
            if (target == null)
                target = new GlobalConfigInfo();

            // Return empty target if source is null
            if (source == null)
                return target;

            // Map common fields from base configuration
            target.AppName = source.AppName;
            target.MainAppName = source.MainAppName;
            target.ClientVersion = source.ClientVersion;
            target.InstallPath = source.InstallPath;
            target.UpdateLogUrl = source.UpdateLogUrl;
            target.AppSecretKey = source.AppSecretKey;
            target.BlackFiles = source.BlackFiles;
            target.BlackFormats = source.BlackFormats;
            target.SkipDirectorys = source.SkipDirectorys;
            target.ReportUrl = source.ReportUrl;
            target.Bowl = source.Bowl;
            target.Scheme = source.Scheme;
            target.Token = source.Token;
            target.Script = source.Script;

            // Map GlobalConfigInfo-specific fields
            target.UpdateUrl = source.UpdateUrl;
            target.UpgradeClientVersion = source.UpgradeClientVersion;
            target.ProductId = source.ProductId;

            return target;
        }

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

            // Create ProcessInfo with all required parameters in a single location
            // This replaces the error-prone manual parameter passing in GeneralClientBootstrap
            return new ProcessInfo(
                appName: source.MainAppName,              // Maps MainAppName to ProcessInfo.AppName
                installPath: source.InstallPath,
                currentVersion: source.ClientVersion,     // Maps ClientVersion to ProcessInfo.CurrentVersion
                lastVersion: source.LastVersion,          // Computed value set before calling this method
                updateLogUrl: source.UpdateLogUrl,
                compressEncoding: source.Encoding,        // Computed value set before calling this method
                compressFormat: source.Format,            // Computed value set before calling this method
                downloadTimeOut: source.DownloadTimeOut,  // Computed value set before calling this method
                appSecretKey: source.AppSecretKey,
                updateVersions: updateVersions,           // From API response
                reportUrl: source.ReportUrl,
                backupDirectory: source.BackupDirectory,  // Computed value set before calling this method
                bowl: source.Bowl,
                scheme: source.Scheme,
                token: source.Token,
                script: source.Script,
                blackFileFormats: blackFileFormats,       // From BlackListManager
                blackFiles: blackFiles,                   // From BlackListManager
                skipDirectories: skipDirectories          // From BlackListManager
            );
        }

        /// <summary>
        /// Copies common configuration fields from a base configuration object to another.
        /// This utility method helps maintain consistency when transferring configuration data.
        /// </summary>
        /// <typeparam name="TSource">The source configuration type (must inherit from BaseConfigInfo).</typeparam>
        /// <typeparam name="TTarget">The target configuration type (must inherit from BaseConfigInfo).</typeparam>
        /// <param name="source">The source configuration object to copy from.</param>
        /// <param name="target">The target configuration object to copy to.</param>
        public static void CopyBaseFields<TSource, TTarget>(TSource source, TTarget target)
            where TSource : BaseConfigInfo
            where TTarget : BaseConfigInfo
        {
            if (source == null || target == null)
                return;

            target.AppName = source.AppName;
            target.MainAppName = source.MainAppName;
            target.InstallPath = source.InstallPath;
            target.UpdateLogUrl = source.UpdateLogUrl;
            target.AppSecretKey = source.AppSecretKey;
            target.ClientVersion = source.ClientVersion;
            target.BlackFiles = source.BlackFiles;
            target.BlackFormats = source.BlackFormats;
            target.SkipDirectorys = source.SkipDirectorys;
            target.ReportUrl = source.ReportUrl;
            target.Bowl = source.Bowl;
            target.Scheme = source.Scheme;
            target.Token = source.Token;
            target.Script = source.Script;
        }
    }
}
