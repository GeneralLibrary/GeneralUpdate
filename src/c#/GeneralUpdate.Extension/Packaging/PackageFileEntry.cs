using System;

namespace MyApp.Extensions.Packaging
{
    /// <summary>
    /// Represents a file entry within a plugin package, providing indexing and metadata for package files.
    /// </summary>
    public class PackageFileEntry
    {
        /// <summary>
        /// Gets or sets the relative path of the file within the package.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the hash of the file for integrity verification.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the hash algorithm used (e.g., "SHA256", "MD5").
        /// </summary>
        public string HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the file.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the file is compressed.
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// Gets or sets the original size of the file before compression.
        /// </summary>
        public long OriginalSize { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp of the file.
        /// </summary>
        public DateTime LastModified { get; set; }
    }
}
