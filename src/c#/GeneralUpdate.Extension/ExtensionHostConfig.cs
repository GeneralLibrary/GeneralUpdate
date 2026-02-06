using System;

namespace GeneralUpdate.Extension
{
    /// <summary>
    /// Configuration settings for initializing a GeneralExtensionHost instance.
    /// Encapsulates all parameters required to set up the extension host environment.
    /// </summary>
    public class ExtensionHostConfig
    {
        /// <summary>
        /// Gets or sets the current host application version.
        /// This is required and used for compatibility checking.
        /// </summary>
        public Version HostVersion { get; set; } = null!;

        /// <summary>
        /// Gets or sets the base directory for extension installations.
        /// This is the root directory where extensions will be installed.
        /// </summary>
        public string InstallBasePath { get; set; } = null!;

        /// <summary>
        /// Gets or sets the directory for downloading extension packages.
        /// Downloaded packages are temporarily stored here before installation.
        /// </summary>
        public string DownloadPath { get; set; } = null!;

        /// <summary>
        /// Gets or sets the server URL for extension queries and downloads.
        /// This is the base URL used to construct Query and Download endpoints.
        /// Example: "https://your-server.com/api/extensions"
        /// </summary>
        public string ServerUrl { get; set; } = null!;

        /// <summary>
        /// Gets or sets the target platform (Windows/Linux/macOS).
        /// Defaults to Windows if not specified.
        /// </summary>
        public Metadata.TargetPlatform TargetPlatform { get; set; } = Metadata.TargetPlatform.Windows;

        /// <summary>
        /// Gets or sets the download timeout in seconds.
        /// Defaults to 300 seconds (5 minutes) if not specified.
        /// </summary>
        public int DownloadTimeout { get; set; } = 300;

        /// <summary>
        /// Gets or sets the optional HTTP authentication scheme (e.g., "Bearer", "Basic").
        /// When set along with AuthToken, enables authenticated downloads.
        /// </summary>
        public string? AuthScheme { get; set; }

        /// <summary>
        /// Gets or sets the optional HTTP authentication token.
        /// When set along with AuthScheme, enables authenticated downloads.
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Validates that all required properties are set.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when required properties are null or empty.</exception>
        public void Validate()
        {
            if (HostVersion == null)
                throw new ArgumentNullException(nameof(HostVersion));
            if (string.IsNullOrWhiteSpace(InstallBasePath))
                throw new ArgumentNullException(nameof(InstallBasePath));
            if (string.IsNullOrWhiteSpace(DownloadPath))
                throw new ArgumentNullException(nameof(DownloadPath));
            if (string.IsNullOrWhiteSpace(ServerUrl))
                throw new ArgumentNullException(nameof(ServerUrl));
        }
    }
}
