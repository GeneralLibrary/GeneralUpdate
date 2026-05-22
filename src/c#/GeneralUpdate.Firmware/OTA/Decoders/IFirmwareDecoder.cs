using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;

namespace GeneralUpdate.Firmware.OTA.Decoders
{
    /// <summary>
    /// Interface for firmware format decoders.
    /// Each implementation parses a specific firmware file format
    /// and returns the unified raw byte stream ready for flashing.
    /// 
    /// <para>
    /// Implementations:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>RawDecoder</c> — .bin/.img, no transformation</description></item>
    ///   <item><description><c>IntelHexDecoder</c> — .hex, ASCII address records + CRC-8</description></item>
    ///   <item><description><c>SRecordDecoder</c> — .srec/.s19, Motorola S-Records + CRC</description></item>
    ///   <item><description><c>AndroidSparseDecoder</c> — .sparse, chunk-based sparse image</description></item>
    /// </list>
    /// </summary>
    public interface IFirmwareDecoder
    {
        /// <summary>
        /// Gets the firmware format that this decoder handles.
        /// </summary>
        FirmwareFormat Format { get; }

        /// <summary>
        /// Decodes the firmware file at the given path into a raw byte array.
        /// Validates internal checksums and resolves address gaps.
        /// </summary>
        /// <param name="filePath">The full path to the firmware file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The decoded raw firmware bytes ready for flashing.</returns>
        Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken);
    }
}
