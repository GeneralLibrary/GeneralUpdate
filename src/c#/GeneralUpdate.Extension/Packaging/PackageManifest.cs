using System;
using System.Collections.Generic;

namespace MyApp.Extensions.Packaging
{
    /// <summary>
    /// Represents the manifest of a plugin package, containing metadata, runtime configuration, 
    /// dependencies, permissions, and signatures.
    /// </summary>
    public class PackageManifest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the package.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version of the package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the author of the package.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the description of the package.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the entry point of the package.
        /// </summary>
        public string Entrypoint { get; set; }

        /// <summary>
        /// Gets or sets the runtime type required by the package.
        /// </summary>
        public string Runtime { get; set; }

        /// <summary>
        /// Gets or sets the engine version compatibility information.
        /// </summary>
        public string Engine { get; set; }

        /// <summary>
        /// Gets or sets the compatibility information for the package.
        /// </summary>
        public string Compatibility { get; set; }

        /// <summary>
        /// Gets or sets the list of dependencies required by the package.
        /// </summary>
        public List<string> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the list of permissions required by the package.
        /// </summary>
        public List<string> Permissions { get; set; }

        /// <summary>
        /// Gets or sets the extension points defined by the package.
        /// </summary>
        public Dictionary<string, object> ExtensionPoints { get; set; }

        /// <summary>
        /// Gets or sets the signature information for package verification.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Gets or sets the format version of the package.
        /// </summary>
        public string FormatVersion { get; set; }
    }
}
