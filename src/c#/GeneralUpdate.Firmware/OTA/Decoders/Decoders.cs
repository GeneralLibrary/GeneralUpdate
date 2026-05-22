using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;

namespace GeneralUpdate.Firmware.OTA.Decoders
{
    /// <summary>
    /// Decoder for raw binary firmware files (.bin, .img, .rom, .fw).
    /// Simply reads the file contents as-is without any transformation.
    /// </summary>
    internal class RawDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.Raw;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(File.ReadAllBytes(filePath));
        }
    }

    /// <summary>
    /// Stub: Intel HEX format decoder.
    /// Full implementation will be provided in a subsequent PR.
    /// </summary>
    internal class IntelHexDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.IntelHex;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            // TODO: Implement Intel HEX parsing (ASCII address records + CRC-8)
            throw new System.NotImplementedException(
                "Intel HEX decoder is not yet implemented. It will parse ASCII address records with CRC-8 validation.");
        }
    }

    /// <summary>
    /// Stub: Motorola S-Record format decoder.
    /// Full implementation will be provided in a subsequent PR.
    /// </summary>
    internal class SRecordDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.SRecord;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            // TODO: Implement S-Record parsing (S0/S1/S2/S3/S5/S7/S8/S9 records)
            throw new System.NotImplementedException(
                "S-Record decoder is not yet implemented. It will parse Motorola S-Records with CRC validation.");
        }
    }

    /// <summary>
    /// Stub: Android sparse image format decoder.
    /// Full implementation will be provided in a subsequent PR.
    /// </summary>
    internal class AndroidSparseDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.AndroidSparse;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            // TODO: Implement Android sparse image parsing (chunk-based with skip blocks)
            throw new System.NotImplementedException(
                "Android sparse decoder is not yet implemented. It will expand sparse images with skip blocks.");
        }
    }
}
