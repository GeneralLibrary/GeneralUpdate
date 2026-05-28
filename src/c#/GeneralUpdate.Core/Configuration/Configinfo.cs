using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Update parameter configuration class for external API callers.
    ///     Designed specifically for external consumers to configure the core parameters required for update behavior.
    ///     Inherits from <see cref="BaseConfigInfo" /> to reuse common fields, reducing duplication and improving maintainability.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Configinfo</c> is the entry-point configuration object for the update workflow, constructed by
    ///         <see cref="ConfiginfoBuilder" /> using the builder pattern. Once built, it is mapped to the internal
    ///         runtime configuration <see cref="GlobalConfigInfo" /> via <see cref="ConfigurationMapper.MapToGlobalConfigInfo" />
    ///         for use by the update pipeline.
    ///     </para>
    ///     <para>
    ///         Calling the <see cref="Validate" /> method performs completeness validation on all required fields,
    ///         ensuring that key parameters such as <c>UpdateUrl</c>, <c>MainAppName</c>, and <c>ClientVersion</c>
    ///         are not empty or incorrectly formatted.
    ///     </para>
    /// </remarks>
    /// <seealso cref="BaseConfigInfo" />
    /// <seealso cref="GlobalConfigInfo" />
    /// <seealso cref="ConfiginfoBuilder" />
    /// <seealso cref="ConfigurationMapper" />
    public class Configinfo : BaseConfigInfo
    {
        /// <summary>
        ///     The API endpoint URL used to check for available updates.
        ///     The client queries this URL to determine whether a new version is available.
        /// </summary>
        /// <remarks>
        ///     This property is required. The <see cref="Validate" /> method checks that it is a valid absolute URI.
        ///     If it is not configured or is malformed, an <see cref="ArgumentException" /> is thrown.
        /// </remarks>
        public string UpdateUrl { get; set; }

        /// <summary>
        ///     The current version number of the updater (the update client itself).
        ///     This version number enables independent upgrades of the updater itself, decoupled from the main application's version management.
        /// </summary>
        /// <remarks>
        ///     By comparing <c>UpgradeClientVersion</c> with the latest version returned by the server,
        ///     the system determines whether the updater itself needs to be upgraded first.
        /// </remarks>
        public string UpgradeClientVersion { get; set; }

        /// <summary>
        ///     The unique product identifier used for tracking and update management.
        ///     Multiple products can share the same update infrastructure and are distinguished by different product IDs.
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        ///     Validates that all required fields of the configuration object are present and correctly formatted.
        /// </summary>
        /// <remarks>
        ///     <para>The method performs validation on the following fields:</para>
        ///     <list type="bullet">
        ///         <item>
        ///             <c>UpdateUrl</c>: Must not be empty and must be a valid absolute URI.</item>
        ///         <item>
        ///             <c>UpdateLogUrl</c>: If set, must be a valid absolute URI.</item>
        ///         <item>
        ///             <c>UpdateAppName</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>MainAppName</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>AppSecretKey</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>ClientVersion</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>InstallPath</c>: Must not be empty.</item>
        ///     </list>
        ///     <para>
        ///         This method is typically called at the end of the <see cref="ConfiginfoBuilder.Build" /> method
        ///         to ensure the constructed configuration object is complete and valid.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     Thrown when any required field is null, empty, consists only of whitespace, or is malformed.
        ///     The exception message indicates which specific field failed validation.
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UpdateUrl) || !Uri.IsWellFormedUriString(UpdateUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateUrl");

            if (!string.IsNullOrWhiteSpace(UpdateLogUrl) && !Uri.IsWellFormedUriString(UpdateLogUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateLogUrl");

            if (string.IsNullOrWhiteSpace(UpdateAppName))
                throw new ArgumentException("UpdateAppName cannot be empty");

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
