namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Enumerates the firmware file formats supported for flashing.
    /// Used by <see cref="FirmwareConfig"/> to specify the expected format
    /// and by the decoder layer to select the appropriate parser.
    /// 
    /// <para>
    /// Set to <see cref="Auto"/> to auto-detect the format based on file extension
    /// and file header magic bytes.
    /// </para>
    /// </summary>
    public enum FirmwareFormat
    {
        /// <summary>
        /// Auto-detect the firmware format based on file extension and magic bytes.
        /// Recommended for most scenarios.
        /// </summary>
        Auto,

        /// <summary>
        /// Raw binary image (.bin, .img).
        /// No parsing required — written directly to the device.
        /// </summary>
        Raw,

        /// <summary>
        /// Intel HEX format (.hex).
        /// ASCII-encoded with address records and CRC-8 per line.
        /// Common in MCU firmware distribution.
        /// </summary>
        IntelHex,

        /// <summary>
        /// Motorola S-Record format (.srec, .s19, .s28).
        /// ASCII-encoded with address records and CRC.
        /// Used in automotive and embedded systems.
        /// </summary>
        SRecord,

        /// <summary>
        /// Vela FlashPack format (.fpk).
        /// Tar-based container with gzip-compressed payload and metadata.
        /// Processed by the vela native library via P/Invoke.
        /// </summary>
        FlashPack,

        /// <summary>
        /// UEFI firmware capsule format (.cap, .uefi).
        /// Windows-only: submitted via DeviceIoControl.
        /// </summary>
        UefiCapsule,

        /// <summary>
        /// Android sparse image format (.sparse, .sparseimg).
        /// Contains skip blocks for efficient sparse flash writing.
        /// </summary>
        AndroidSparse
    }
}
