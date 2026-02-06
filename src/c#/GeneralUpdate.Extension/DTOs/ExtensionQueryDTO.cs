namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Extension query data transfer object
    /// </summary>
    public class ExtensionQueryDTO
    {
        /// <summary>
        /// Page number for pagination (default: 1)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 10)
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Filter by extension name (optional)
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Filter by publisher (optional)
        /// </summary>
        public string? Publisher { get; set; }

        /// <summary>
        /// Filter by category (optional)
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Filter by target platform (optional)
        /// </summary>
        public Metadata.TargetPlatform? TargetPlatform { get; set; }

        /// <summary>
        /// Host version for compatibility checking (optional)
        /// </summary>
        public string? HostVersion { get; set; }

        /// <summary>
        /// Include pre-release versions (default: false)
        /// </summary>
        public bool IncludePreRelease { get; set; } = false;

        /// <summary>
        /// Search term for general search (optional)
        /// </summary>
        public string? SearchTerm { get; set; }
    }
}
