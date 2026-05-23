using System.IO;
using System.Threading;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// Deflate compression provider using <see cref="System.IO.Compression.DeflateStream"/> (BCL, universally available).
    /// Format version byte: 0x01.
    /// </summary>
    /// <remarks>
    /// Compared to BZip2:
    /// - Decompression is 2-3x faster (good for client-side patch application).
    /// - Compression ratio is comparable for binary data.
    /// - Available on all target frameworks including netstandard2.0 with zero extra dependencies.
    /// </remarks>
    public sealed class DeflateCompressionProvider : ICompressionProvider
    {
        private readonly System.IO.Compression.CompressionLevel _compressionLevel;

        /// <summary>
        /// Initialises a new Deflate compression provider with the specified quality.
        /// </summary>
        /// <param name="optimalLevel">
        /// If true, uses CompressionLevel.Optimal (best compression, slower).
        /// If false, uses CompressionLevel.Fastest (faster compression, slightly larger output).
        /// Default: true (optimal).
        /// </param>
        public DeflateCompressionProvider(bool optimalLevel = true)
        {
            _compressionLevel = optimalLevel
                ? System.IO.Compression.CompressionLevel.Optimal
                : System.IO.Compression.CompressionLevel.Fastest;
        }

        /// <inheritdoc/>
        public byte FormatVersion => 0x01;

        /// <inheritdoc/>
        public Stream CreateCompressStream(Stream output, CancellationToken cancellationToken = default)
        {
            // DeflateStream: leaveOpen=true so disposing the wrapper does not close the underlying stream.
            return new System.IO.Compression.DeflateStream(output, _compressionLevel, leaveOpen: true);
        }

        /// <inheritdoc/>
        public Stream CreateDecompressStream(Stream input, CancellationToken cancellationToken = default)
        {
            return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        }
    }
}
