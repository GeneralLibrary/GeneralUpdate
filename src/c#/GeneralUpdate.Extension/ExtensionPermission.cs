namespace MyApp.Extensions
{
    /// <summary>
    /// Represents a permission required by an extension.
    /// </summary>
    public class ExtensionPermission
    {
        /// <summary>
        /// Gets or sets the type of permission (e.g., "FileSystem", "Network", "System").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the scope or target of the permission (e.g., specific paths, URLs, or system resources).
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the access level required (e.g., "Read", "Write", "Execute", "Full").
        /// </summary>
        public string AccessLevel { get; set; }

        /// <summary>
        /// Gets or sets a description of why this permission is required.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this permission is mandatory.
        /// </summary>
        public bool IsMandatory { get; set; }
    }
}
