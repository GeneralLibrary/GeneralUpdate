#![forbid(unsafe_code)]
#![doc = "Firmware flash-to-block-device module for Vela OTA."]
#![doc = ""]
#![doc = "Provides `BlockDeviceWriter` for chunked writes to raw block devices"]
#![doc = "and `FpkInstaller` for installing `.fpk` bundles to devices."]

use std::io;
use thiserror::Error;

pub mod direct_writer;
pub mod fpk_installer;

pub use direct_writer::BlockDeviceWriter;
pub use fpk_installer::FpkInstaller;

/// Errors that can occur during flash operations.
#[derive(Error, Debug)]
pub enum FlasherError {
    #[error("Failed to open device {device}: {source}")]
    OpenFailed {
        device: String,
        #[source]
        source: io::Error,
    },

    #[error("Write to device {device} at offset {offset} failed: {source}")]
    WriteFailed {
        device: String,
        offset: u64,
        #[source]
        source: io::Error,
    },

    #[error("Short write: expected {expected} bytes, wrote {actual}")]
    ShortWrite { expected: usize, actual: usize },

    #[error("Device {device} too small: need {required} bytes, have {available}")]
    DeviceTooSmall {
        device: String,
        required: u64,
        available: u64,
    },

    #[error("Hash mismatch after write: expected {expected}, got {actual}")]
    HashMismatch { expected: String, actual: String },

    #[error("FlashPack error: {0}")]
    FpkError(#[from] vela_flashpack::FlashPackError),

    #[error("IO error: {0}")]
    Io(#[from] io::Error),
}

/// Result type alias for flash operations.
pub type FlasherResult<T> = Result<T, FlasherError>;

/// Configuration for flash operations.
///
/// The `device_path` field specifies the target block device.
/// Chunked writes with fsync and optional read-back verification
/// provide integrity guarantees suitable for OTA updates.
#[derive(Debug, Clone)]
pub struct FlashConfig {
    /// Path to the block device (e.g., `/dev/mmcblk0p3`).
    pub device_path: String,
    /// Whether to call `fsync` after each chunk write.
    pub sync_after_chunk: bool,
    /// Size of each write chunk in bytes (default: 1 MiB).
    pub chunk_size: usize,
    /// Whether to read back and verify each chunk after writing.
    pub verify_after_write: bool,
}

impl Default for FlashConfig {
    fn default() -> Self {
        Self {
            device_path: String::new(),
            sync_after_chunk: true,
            chunk_size: 1024 * 1024, // 1 MiB
            verify_after_write: true,
        }
    }
}

impl FlashConfig {
    /// Create a new `FlashConfig` with the given device path.
    pub fn new(device_path: impl Into<String>) -> Self {
        Self {
            device_path: device_path.into(),
            ..Default::default()
        }
    }

    /// Builder: set the device path.
    pub fn device_path(mut self, path: impl Into<String>) -> Self {
        self.device_path = path.into();
        self
    }

    /// Builder: enable or disable fsync after each chunk.
    pub fn sync_after_chunk(mut self, sync: bool) -> Self {
        self.sync_after_chunk = sync;
        self
    }

    /// Builder: set the chunk size for writes.
    pub fn chunk_size(mut self, size: usize) -> Self {
        self.chunk_size = size;
        self
    }

    /// Builder: enable or disable read-back verification after each chunk.
    pub fn verify_after_write(mut self, verify: bool) -> Self {
        self.verify_after_write = verify;
        self
    }
}

/// Callback for progress reporting during flash operations.
///
/// Arguments: `(bytes_written, total_bytes)`.
pub type ProgressCallback = Box<dyn Fn(u64, u64) + Send + Sync>;
