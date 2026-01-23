using System;
using System.Collections.Generic;

namespace MyApp.Extensions
{
    /// <summary>
    /// Represents the manifest of an extension, containing all metadata and configuration information.
    /// </summary>
    public class ExtensionManifest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the extension.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the extension.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version of the extension.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the author or publisher of the extension.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the description of the extension.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the entry point of the extension.
        /// </summary>
        public string Entrypoint { get; set; }

        /// <summary>
        /// Gets or sets the runtime type required by the extension.
        /// </summary>
        public string Runtime { get; set; }

        /// <summary>
        /// Gets or sets the engine version compatibility information.
        /// </summary>
        public string Engine { get; set; }

        /// <summary>
        /// Gets or sets the compatibility information for the extension.
        /// </summary>
        public string Compatibility { get; set; }

        /// <summary>
        /// Gets or sets the list of dependencies required by the extension.
        /// </summary>
        public List<ExtensionDependency> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the list of permissions required by the extension.
        /// </summary>
        public List<ExtensionPermission> Permissions { get; set; }

        /// <summary>
        /// Gets or sets the icon path for the extension.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets or sets the license identifier for the extension.
        /// </summary>
        public string License { get; set; }

        /// <summary>
        /// Gets or sets the homepage URL for the extension.
        /// </summary>
        public string Homepage { get; set; }

        /// <summary>
        /// Gets or sets the repository URL for the extension.
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// Gets or sets the tags or keywords associated with the extension.
        /// </summary>
        public List<string> Tags { get; set; }
    }
}
