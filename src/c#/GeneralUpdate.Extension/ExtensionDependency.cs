namespace MyApp.Extensions
{
    /// <summary>
    /// Represents a dependency on another extension.
    /// </summary>
    public class ExtensionDependency
    {
        /// <summary>
        /// Gets or sets the unique identifier of the dependency.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version range required for the dependency (e.g., ">=1.0.0 <2.0.0").
        /// </summary>
        public string VersionRange { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Gets or sets the display name of the dependency.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a description of why this dependency is required.
        /// </summary>
        public string Reason { get; set; }
    }
}
