using System;
using System.Collections.Generic;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// User-facing configuration class for update parameters.
    /// This class is designed for external API consumers to configure update behavior.
    /// Inherits common fields from BaseConfigInfo to reduce duplication and improve maintainability.
    /// </summary>
    public class Configinfo : BaseConfigInfo
    {
        /// <summary>
        /// The API endpoint URL for checking available updates.
        /// The client queries this URL to determine if new versions are available.
        /// </summary>
        public string UpdateUrl { get; set; }

        /// <summary>
        /// The current version of the upgrade application (the updater itself).
        /// This allows the updater tool to be updated separately from the main application.
        /// </summary>
        public string UpgradeClientVersion { get; set; }

        /// <summary>
        /// The unique product identifier used for tracking and update management.
        /// Multiple products can share the same update infrastructure using different IDs.
        /// </summary>
        public string ProductId { get; set; }

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