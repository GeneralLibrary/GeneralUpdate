using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// Defines a pluggable compression strategy for patch data.
    /// Each patch format version may use a different compression algorithm
    /// (BZip2 for legacy, Brotli for new, Deflate for compatibility).
    /// </summary>
    public interface ICompressionProvider
    {
        /// <summary>
        /// Gets the format identifier byte written into the patch header for version detection.
        /// 0x00 = BZip2 (legacy BSDIFF), 0x01 = Brotli, 0x02+ = reserved.
        /// </summary>
        byte FormatVersion { get; }

        /// <summary>
        /// Wraps <paramref name="output"/> in a compression stream for writing patch data.
        /// The caller owns the returned stream and must dispose it.
        /// </summary>
        /// <param name="output">The underlying output stream.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A compression stream that writes compressed data to <paramref name="output"/>.</returns>
        Stream CreateCompressStream(Stream output, CancellationToken cancellationToken = default);

        /// <summary>
        /// Wraps <paramref name="input"/> in a decompression stream for reading patch data.
        /// The caller owns the returned stream and must dispose it.
        /// </summary>
        /// <param name="input">The underlying input stream containing compressed data.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A decompression stream that reads decompressed data from <paramref name="input"/>.</returns>
        Stream CreateDecompressStream(Stream input, CancellationToken cancellationToken = default);
    }
}
