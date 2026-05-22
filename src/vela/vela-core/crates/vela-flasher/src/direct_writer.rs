//! Block-device writer: chunked writes with fsync, read-back verify, and SHA-256 tracking.
//!
//! `BlockDeviceWriter` is the low-level I/O layer for writing firmware images
//! to raw block devices (or regular files during testing). It guarantees:
//!
//! - **Chunked writes** — configurable chunk size (default 1 MiB) to limit the
//!   amount of data in flight.
//! - **fsync** — optional per-chunk `sync_all()` to flush OS buffers.
//! - **Read-back verification** — optional per-chunk read-and-compare to detect
//!   silent data corruption.
//! - **SHA-256 tracking** — incremental hash of all bytes written, available
//!   for post-write integrity checks.

use std::fs::{File, OpenOptions};
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::Path;

use sha2::{Digest, Sha256};
use tracing::{debug, error, info, instrument, trace};

use crate::{FlashConfig, FlasherError, FlasherResult, ProgressCallback};

/// Writes firmware images to a block device with chunked I/O,
/// fsync, read-back verification, and SHA-256 tracking.
pub struct BlockDeviceWriter {
    config: FlashConfig,
    file: Option<File>,
    bytes_written: u64,
    hasher: Sha256,
    device_size: u64,
}

impl BlockDeviceWriter {
    /// Create a new writer with the given configuration.
    ///
    /// The device is lazily opened on the first `write_image` call.
    pub fn new(config: FlashConfig) -> Self {
        Self {
            config,
            file: None,
            bytes_written: 0,
            hasher: Sha256::new(),
            device_size: 0,
        }
    }

    /// Write a firmware payload to the configured block device.
    ///
    /// Returns the total number of bytes written.
    ///
    /// # Errors
    ///
    /// Returns `FlasherError::OpenFailed` if the device cannot be opened.
    /// Returns `FlasherError::DeviceTooSmall` if the payload exceeds the device capacity.
    /// Returns `FlasherError::WriteFailed` if a write or read-back verification fails.
    #[instrument(skip(self, payload, progress))]
    pub fn write_image(
        &mut self,
        payload: &[u8],
        progress: Option<&ProgressCallback>,
    ) -> FlasherResult<u64> {
        if self.config.device_path.is_empty() {
            return Err(FlasherError::OpenFailed {
                device: "<empty>".into(),
                source: std::io::Error::new(
                    std::io::ErrorKind::NotFound,
                    "device path is empty",
                ),
            });
        }

        let _ = self.open_device()?;
        self.verify_capacity(payload.len() as u64)?;

        // Take ownership of the file handle to avoid double-borrow across iterations
        let mut file = self.file.take().unwrap();
        let total = payload.len() as u64;
        let chunk_size = self.config.chunk_size;

        for (i, chunk) in payload.chunks(chunk_size).enumerate() {
            let offset = (i * chunk_size) as u64;
            self.write_chunk_inner(&mut file, offset, chunk)?;

            if let Some(cb) = progress {
                cb(self.bytes_written, total);
            }
        }

        // Final fsync to flush all pending writes
        file.sync_all().map_err(FlasherError::Io)?;

        // Return the file handle
        self.file = Some(file);

        info!(bytes = self.bytes_written, "Image write completed");
        Ok(self.bytes_written)
    }

    /// Return the SHA-256 hex string of all data written so far.
    ///
    /// Returns the SHA-256 of the empty string if no data has been written yet.
    pub fn sha256_checksum(&self) -> Option<String> {
        let hash = self.hasher.clone().finalize();
        Some(hex::encode(hash))
    }

    /// Verify that the written data matches the expected SHA-256 hex checksum.
    pub fn verify_checksum(&self, expected: &str) -> bool {
        match self.sha256_checksum() {
            Some(computed) => computed == expected,
            None => false,
        }
    }

    /// Return the total number of bytes written so far.
    pub fn bytes_written(&self) -> u64 {
        self.bytes_written
    }

    // ── Private helpers ────────────────────────────────────────────────

    /// Open the block device (or regular file) for read-write access.
    /// Sets `self.device_size` from the file metadata.
    fn open_device(&mut self) -> FlasherResult<()> {
        if self.file.is_some() {
            return Ok(());
        }

        let path = Path::new(&self.config.device_path);
        debug!(device = %self.config.device_path, "Opening device for writing");

        let file = OpenOptions::new()
            .write(true)
            .read(true)
            .open(path)
            .map_err(|e| FlasherError::OpenFailed {
                device: self.config.device_path.clone(),
                source: e,
            })?;

        let metadata = file.metadata().map_err(FlasherError::Io)?;
        self.device_size = metadata.len();

        trace!(size = self.device_size, "Device opened successfully");
        self.file = Some(file);
        Ok(())
    }

    /// Check that the device has enough capacity for the payload.
    fn verify_capacity(&self, required: u64) -> FlasherResult<()> {
        // A size of 0 means the device size is unknown (e.g., a special
        // block device that doesn't report size). We allow writes in that case
        // and rely on the kernel to enforce limits.
        if self.device_size > 0 && required > self.device_size {
            return Err(FlasherError::DeviceTooSmall {
                device: self.config.device_path.clone(),
                required,
                available: self.device_size,
            });
        }
        debug!(
            required,
            available = self.device_size,
            "Capacity verification passed"
        );
        Ok(())
    }

    /// Write a single chunk to the device at the given offset.
    /// Updates the internal SHA-256 hasher and bytes_written counter.
    /// Optionally fsyncs and read-back verifies, depending on config.
    fn write_chunk_inner(&mut self, file: &mut File, offset: u64, data: &[u8]) -> FlasherResult<()> {
        file.seek(SeekFrom::Start(offset)).map_err(FlasherError::Io)?;

        let written = file.write(data).map_err(|e| FlasherError::WriteFailed {
            device: self.config.device_path.clone(),
            offset,
            source: e,
        })?;

        if written != data.len() {
            return Err(FlasherError::ShortWrite {
                expected: data.len(),
                actual: written,
            });
        }

        // Update the incremental hasher
        self.hasher.update(data);
        self.bytes_written += written as u64;

        // Optional per-chunk fsync
        if self.config.sync_after_chunk {
            file.sync_all().map_err(FlasherError::Io)?;
        }

        // Optional read-back verification
        if self.config.verify_after_write {
            self.verify_chunk(file, offset, data)?;
        }

        trace!(offset, size = written, "Chunk written successfully");
        Ok(())
    }

    /// Read back a chunk from the device and compare it with the expected data.
    fn verify_chunk(&self, file: &mut File, offset: u64, expected: &[u8]) -> FlasherResult<()> {
        let mut buf = vec![0u8; expected.len()];

        file.seek(SeekFrom::Start(offset)).map_err(FlasherError::Io)?;
        file.read_exact(&mut buf).map_err(FlasherError::Io)?;

        if buf != expected {
            error!(
                offset,
                size = expected.len(),
                "Read-back verification failed: data mismatch"
            );
            return Err(FlasherError::WriteFailed {
                device: self.config.device_path.clone(),
                offset,
                source: std::io::Error::new(
                    std::io::ErrorKind::Other,
                    "read-back data mismatch",
                ),
            });
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::NamedTempFile;

    /// Create a writer configured to use a temporary file as the "device".
    fn writer_for_temp(file: &NamedTempFile) -> BlockDeviceWriter {
        let config = FlashConfig::new(file.path().to_string_lossy().as_ref())
            .sync_after_chunk(false)
            .verify_after_write(false);
        BlockDeviceWriter::new(config)
    }

    #[test]
    fn test_write_small_payload() {
        let tmp = NamedTempFile::new().unwrap();
        let mut writer = writer_for_temp(&tmp);

        let payload = b"Hello, Vela OTA! This is a firmware payload.";
        let written = writer.write_image(payload, None).unwrap();
        assert_eq!(written, payload.len() as u64);
        assert_eq!(writer.bytes_written(), payload.len() as u64);
    }

    #[test]
    fn test_write_with_progress() {
        let tmp = NamedTempFile::new().unwrap();
        let mut writer = writer_for_temp(&tmp);

        let payload = vec![0xAAu8; 5000];
        let callback_calls = std::sync::Arc::new(std::sync::Mutex::new(Vec::<(u64, u64)>::new()));
        let calls_ref = callback_calls.clone();
        let cb: ProgressCallback = Box::new(move |written, total| {
            calls_ref.lock().unwrap().push((written, total));
        });

        writer.write_image(&payload, Some(&cb)).unwrap();
        assert!(!callback_calls.lock().unwrap().is_empty(), "Progress callback was never called");
    }

    #[test]
    fn test_write_with_readback_verify() {
        let tmp = NamedTempFile::new().unwrap();
        let config = FlashConfig::new(tmp.path().to_string_lossy().as_ref())
            .sync_after_chunk(false)
            .verify_after_write(true)
            .chunk_size(64);
        let mut writer = BlockDeviceWriter::new(config);

        let payload = vec![0xBBu8; 256];
        let written = writer.write_image(&payload, None).unwrap();
        assert_eq!(written, 256);
    }

    #[test]
    fn test_device_too_small() {
        let tmp = NamedTempFile::new().unwrap();
        // Write a small file to get a known size
        std::fs::write(tmp.path(), b"tiny").unwrap();

        let config = FlashConfig::new(tmp.path().to_string_lossy().as_ref());
        let mut writer = BlockDeviceWriter::new(config);

        let payload = vec![0u8; 1024 * 1024]; // 1 MiB, definitely larger than 4 bytes
        let result = writer.write_image(&payload, None);
        assert!(result.is_err());
        assert!(
            matches!(result.unwrap_err(), FlasherError::DeviceTooSmall { .. }),
            "Expected DeviceTooSmall error"
        );
    }

    #[test]
    fn test_sha256_tracking() {
        let tmp = NamedTempFile::new().unwrap();
        let mut writer = writer_for_temp(&tmp);

        let payload = b"consistency check payload for sha256";
        writer.write_image(payload, None).unwrap();

        let checksum = writer.sha256_checksum().expect("should have a checksum");
        assert!(!checksum.is_empty());

        // Verify that verify_checksum works
        assert!(writer.verify_checksum(&checksum));
        assert!(!writer.verify_checksum("0000000000000000000000000000000000000000000000000000000000000000"));
    }

    #[test]
    fn test_empty_device_path_is_error() {
        let mut writer = BlockDeviceWriter::new(FlashConfig::default());
        let result = writer.write_image(b"test", None);
        assert!(result.is_err());
    }

    #[test]
    fn test_sha256_returns_empty_hash_before_write() {
        let tmp = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(&tmp);
        // sha256_checksum() returns the hash of empty input when no data written
        let checksum = writer.sha256_checksum().expect("should return a checksum even for empty input");
        // SHA-256 of empty string
        assert_eq!(checksum, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        assert!(!writer.verify_checksum("anything"));
    }

    #[test]
    fn test_large_chunked_write() {
        let tmp = NamedTempFile::new().unwrap();
        let config = FlashConfig::new(tmp.path().to_string_lossy().as_ref())
            .sync_after_chunk(false)
            .verify_after_write(false)
            .chunk_size(64); // small chunks to exercise chunking
        let mut writer = BlockDeviceWriter::new(config);

        let payload = vec![0xCCu8; 1000]; // 1000 bytes in 64-byte chunks
        let written = writer.write_image(&payload, None).unwrap();
        assert_eq!(written, 1000);
    }

    #[test]
    fn test_write_preserves_payload() {
        let tmp = NamedTempFile::new().unwrap();
        let config = FlashConfig::new(tmp.path().to_string_lossy().as_ref())
            .sync_after_chunk(false)
            .verify_after_write(false);
        let mut writer = BlockDeviceWriter::new(config);

        let payload: Vec<u8> = (0..255).collect(); // 0, 1, 2, ..., 254
        writer.write_image(&payload, None).unwrap();

        // Read back from the file and verify
        let written_data = std::fs::read(tmp.path()).unwrap();
        assert_eq!(&written_data[..payload.len()], payload.as_slice());
    }

    #[test]
    fn test_nonexistent_device() {
        let config = FlashConfig::new("/nonexistent/device/path_xyz_123");
        let mut writer = BlockDeviceWriter::new(config);
        let result = writer.write_image(b"test", None);
        assert!(result.is_err());
    }
}
