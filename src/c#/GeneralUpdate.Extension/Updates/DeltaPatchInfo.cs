using System.Collections.Generic;

namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Represents information about a delta patch, including baseline version, target version, 
    /// patch algorithm, and differential block information.
    /// </summary>
    public class DeltaPatchInfo
    {
        /// <summary>
        /// Gets or sets the baseline version from which the patch is applied.
        /// </summary>
        public string BaselineVersion { get; set; }

        /// <summary>
        /// Gets or sets the target version that will be achieved after applying the patch.
        /// </summary>
        public string TargetVersion { get; set; }

        /// <summary>
        /// Gets or sets the patch algorithm used (e.g., "BSDiff", "Xdelta", "Custom").
        /// </summary>
        public string PatchAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the size of the patch in bytes.
        /// </summary>
        public long PatchSize { get; set; }

        /// <summary>
        /// Gets or sets the list of differential blocks that make up the patch.
        /// </summary>
        public List<DifferentialBlock> DifferentialBlocks { get; set; }

        /// <summary>
        /// Gets or sets the compression method used for the patch (e.g., "gzip", "bz2", "xz").
        /// </summary>
        public string CompressionMethod { get; set; }

        /// <summary>
        /// Gets or sets the hash of the patch file for integrity verification.
        /// </summary>
        public string PatchHash { get; set; }

        /// <summary>
        /// Gets or sets the hash algorithm used (e.g., "SHA256").
        /// </summary>
        public string HashAlgorithm { get; set; }
    }

    /// <summary>
    /// Represents a single differential block within a patch.
    /// </summary>
    public class DifferentialBlock
    {
        /// <summary>
        /// Gets or sets the offset in the source file where the block starts.
        /// </summary>
        public long SourceOffset { get; set; }

        /// <summary>
        /// Gets or sets the length of the block in the source file.
        /// </summary>
        public long SourceLength { get; set; }

        /// <summary>
        /// Gets or sets the offset in the target file where the block should be written.
        /// </summary>
        public long TargetOffset { get; set; }

        /// <summary>
        /// Gets or sets the length of the block in the target file.
        /// </summary>
        public long TargetLength { get; set; }

        /// <summary>
        /// Gets or sets the hash of the block for verification.
        /// </summary>
        public string BlockHash { get; set; }
    }
}
