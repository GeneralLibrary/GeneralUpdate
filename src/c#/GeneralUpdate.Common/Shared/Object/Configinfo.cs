using System;
using System.Collections.Generic;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Global update parameters.
    /// </summary>
    public class Configinfo
    {
        /// <summary>
        /// Update check api address.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        /// API address for reporting update status.
        /// </summary>
        public string ReportUrl { get; set; }

        /// <summary>
        /// Need to start the name of the app.
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// The name of the main application, without .exe.
        /// </summary>
        public string MainAppName { get; set; }

        /// <summary>
        /// Update log web address.
        /// </summary>
        public string UpdateLogUrl { get; set; }

        /// <summary>
        /// application key
        /// </summary>
        public string AppSecretKey { get; set; }

        /// <summary>
        /// Client current version.
        /// </summary>
        public string ClientVersion { get; set; }
        
        /// <summary>
        /// Upgrade Client current version.
        /// </summary>
        public string UpgradeClientVersion { get; set; }

        /// <summary>
        /// installation path (for update file logic).
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Files in the blacklist will skip the update.
        /// </summary>
        public List<string> BlackFiles { get; set; }

        /// <summary>
        /// File formats in the blacklist will skip the update.
        /// </summary>
        public List<string> BlackFormats { get; set; }

        /// <summary>
        /// SkipDirectorys
        /// </summary>
        public List<string> SkipDirectorys { get; set; }

        /// <summary>
        /// Product ID.
        /// </summary>
        public string ProductId { get; set; }

        public string Bowl { get; set; }
        
        public string Scheme { get; set; }
    
        public string Token { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UpdateUrl) || !Uri.IsWellFormedUriString(UpdateUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateUrl");

            if (!string.IsNullOrWhiteSpace(UpdateLogUrl) && !Uri.IsWellFormedUriString(UpdateLogUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateLogUrl");

            if (string.IsNullOrWhiteSpace(AppName))
                throw new ArgumentException("AppName cannot be empty");

            if (string.IsNullOrWhiteSpace(MainAppName))
                throw new ArgumentException("MainAppName cannot be empty");

            if (string.IsNullOrWhiteSpace(AppSecretKey))
                throw new ArgumentException("AppSecretKey cannot be empty");

            if (string.IsNullOrWhiteSpace(ClientVersion))
                throw new ArgumentException("ClientVersion cannot be empty");

            if (string.IsNullOrWhiteSpace(InstallPath))
                throw new ArgumentException("InstallPath cannot be empty");
        }
    }
}