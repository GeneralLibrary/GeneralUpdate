using System.Collections.Generic;

namespace MyApp.Extensions.Security
{
    /// <summary>
    /// Represents enterprise-level policy rules for plugin sources and installations.
    /// </summary>
    public class EnterprisePolicy
    {
        /// <summary>
        /// Gets or sets the list of allowed repository sources.
        /// </summary>
        public List<string> AllowedSources { get; set; }

        /// <summary>
        /// Gets or sets the list of blocked repository sources.
        /// </summary>
        public List<string> BlockedSources { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only signed extensions are allowed.
        /// </summary>
        public bool RequireSignedExtensions { get; set; }

        /// <summary>
        /// Gets or sets the list of trusted certificate thumbprints.
        /// </summary>
        public List<string> TrustedCertificates { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether extensions must be approved before installation.
        /// </summary>
        public bool RequireApproval { get; set; }

        /// <summary>
        /// Gets or sets the list of explicitly allowed extensions.
        /// </summary>
        public List<string> AllowedExtensions { get; set; }

        /// <summary>
        /// Gets or sets the list of explicitly blocked extensions.
        /// </summary>
        public List<string> BlockedExtensions { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed permissions for extensions.
        /// </summary>
        public List<string> MaximumAllowedPermissions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic updates are allowed.
        /// </summary>
        public bool AllowAutomaticUpdates { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether users can install extensions.
        /// </summary>
        public bool AllowUserInstallation { get; set; }

        /// <summary>
        /// Gets or sets the policy enforcement mode (e.g., "Strict", "Lenient", "Audit").
        /// </summary>
        public string EnforcementMode { get; set; }
    }
}
