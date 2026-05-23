using System.IO;
using System.Threading;
using GeneralUpdate.Differential.Binary;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// BZip2 compression provider - preserves backward compatibility with legacy BSDIFF patches.
    /// Format version byte: 0x00.
    /// </summary>
    public sealed class BZip2CompressionProvider : ICompressionProvider
    {
        /// <inheritdoc/>
        public byte FormatVersion => 0x00;

        /// <inheritdoc/>
        public Stream CreateCompressStream(Stream output, CancellationToken cancellationToken = default)
        {
            // BZip2OutputStream is the existing managed BZip2 compressor.
            // IsStreamOwner = false so disposing the wrapper does not close the underlying stream.
            return new BZip2OutputStream(output) { IsStreamOwner = false };
        }

        /// <inheritdoc/>
        public Stream CreateDecompressStream(Stream input, CancellationToken cancellationToken = default)
        {
            // BZip2InputStream only accepts a Stream; the single-parameter constructor is used.
            return new BZip2InputStream(input);
        }
    }
}
