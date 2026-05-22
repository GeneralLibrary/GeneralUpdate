//! FpkInstaller: reads `.fpk` archives, verifies checksums, decompresses payloads,
//! flashes firmware to block devices, and verifies the written hash.
//!
//! The `FpkInstaller` orchestrates the full installation pipeline:
//!
//! 1. Open and parse the `.fpk` archive via `vela_flashpack::FlashPackReader`.
//! 2. Verify the checksums of all archive components.
//! 3. Decompress the gzipped payload.
//! 4. Compute the SHA-256 hash of the decompressed payload.
//! 5. Flash the payload to the target device using `BlockDeviceWriter`.
//! 6. Verify the hash of the written data matches the decompressed payload hash.
//!
//! A convenience function `install_fpk()` bundles all of these steps
//! into a single call for quick integration.

use std::io::Read;
use std::path::Path;

use sha2::{Digest, Sha256};
use tracing::{error, info, instrument, trace};

use crate::direct_writer::BlockDeviceWriter;
use crate::{FlashConfig, FlasherError, FlasherResult, ProgressCallback};
use vela_flashpack::FlashPackReader;

/// Installs a `.fpk` firmware bundle onto a target block device.
///
/// Owns a `BlockDeviceWriter` for the actual I/O and a path to the `.fpk` file.
pub struct FpkInstaller {
    fpk_path: String,
    writer: BlockDeviceWriter,
}

impl FpkInstaller {
    /// Create a new installer for the given `.fpk` file and block device writer.
    pub fn new(fpk_path: impl Into<String>, writer: BlockDeviceWriter) -> Self {
        Self {
            fpk_path: fpk_path.into(),
            writer,
        }
    }

    /// Execute the full installation pipeline.
    ///
    /// 1. Open and parse the `.fpk`.
    /// 2. Verify archive checksums.
    /// 3. Decompress the gzipped payload.
    /// 4. Compute the SHA-256 hash of the decompressed data.
    /// 5. Flash the decompressed payload to the target device.
    /// 6. Verify that the hash of the written data matches.
    ///
    /// Returns the number of decompressed bytes written.
    #[instrument(skip(self, progress), fields(fpk = %self.fpk_path))]
    pub fn install(&mut self, progress: Option<&ProgressCallback>) -> FlasherResult<u64> {
        info!("Starting FPK installation pipeline");

        // 1. Open and parse the .fpk archive
        let fpk_path = Path::new(&self.fpk_path);
        let reader = FlashPackReader::open(fpk_path)?;
        trace!(
            bundle = %reader.header.bundle_name,
            version = %reader.header.bundle_version,
            "FlashPack opened"
        );

        // 2. Verify archive checksums (header + payload)
        let _bundle_hash = reader.verify_checksums()?;
        info!("FlashPack checksum verification passed");

        // 3. Decompress the gzipped payload
        let payload_reader = reader.payload_reader()?;
        let decompressed = Self::decompress_payload(payload_reader)?;
        trace!(
            decompressed_size = decompressed.len(),
            "Payload decompressed"
        );

        // 4. Compute SHA-256 of the decompressed payload
        let decompressed_hash = {
            let mut hasher = Sha256::new();
            hasher.update(&decompressed);
            hex::encode(hasher.finalize())
        };
        trace!(hash = %&decompressed_hash[..16], "Decompressed payload hash computed");

        // 5. Flash the decompressed payload to the device
        let bytes_written = self.writer.write_image(&decompressed, progress)?;
        info!(bytes = bytes_written, "Payload written to device");

        // 6. Verify that the written data hash matches the decompressed payload hash
        let written_hash = self.writer.sha256_checksum().unwrap_or_default();
        if written_hash != decompressed_hash {
            error!(
                expected = %&decompressed_hash[..16],
                actual = %&written_hash[..16],
                "Post-write hash verification failed"
            );
            return Err(FlasherError::HashMismatch {
                expected: decompressed_hash,
                actual: written_hash,
            });
        }

        info!(
            bytes = bytes_written,
            "FPK installation completed successfully"
        );
        Ok(bytes_written)
    }

    /// Decompress a gzip-compressed payload reader into a byte vector.
    fn decompress_payload<R: Read>(reader: R) -> FlasherResult<Vec<u8>> {
        use flate2::read::GzDecoder;

        let mut decoder = GzDecoder::new(reader);
        let mut buf = Vec::new();
        decoder.read_to_end(&mut buf).map_err(FlasherError::Io)?;
        Ok(buf)
    }
}

/// Convenience function to install a `.fpk` to a block device in one call.
///
/// This bundles `FlashConfig` construction, `BlockDeviceWriter` creation,
/// and `FpkInstaller::install()` into a single function for easy integration.
pub fn install_fpk(
    fpk_path: &str,
    device_path: &str,
    progress: Option<&ProgressCallback>,
) -> FlasherResult<u64> {
    let config = FlashConfig::new(device_path);
    let writer = BlockDeviceWriter::new(config);
    let mut installer = FpkInstaller::new(fpk_path, writer);
    installer.install(progress)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::NamedTempFile;
    use vela_flashpack::header::{FpkHeader, PayloadType};

    /// Build a minimal valid `.fpk` file containing gzip-compressed payload data.
    fn build_test_fpk(payload_data: &[u8]) -> (tempfile::TempDir, std::path::PathBuf) {
        let dir = tempfile::tempdir().unwrap();
        let fpk_path = dir.path().join("test.fpk");

        let file = File::create(&fpk_path).unwrap();
        let mut archive = tar::Builder::new(file);

        // 1. Compress the payload
        use flate2::write::GzEncoder;
        use flate2::Compression;
        let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
        encoder.write_all(payload_data).unwrap();
        let compressed = encoder.finish().unwrap();

        let payload_sha256 = {
            let mut h = Sha256::new();
            h.update(&compressed);
            hex::encode(h.finalize())
        };

        // 2. Build the header
        let header = FpkHeader {
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            bundle_name: "test-install-bundle".into(),
            bundle_version: "2.0.0".into(),
            compatible_slots: vec!["test-slot".into()],
            payload_type: PayloadType::FullImage,
            payload_size: compressed.len() as u64,
            requires_version: "1.0.0".into(),
            created_at: "2026-05-22T00:00:00Z".into(),
            builder_id: "test-ci".into(),
            compat_flags: vec![],
        };
        let header_json = serde_json::to_vec_pretty(&header).unwrap();
        let header_sha256 = {
            let mut h = Sha256::new();
            h.update(&header_json);
            hex::encode(h.finalize())
        };

        // 3. Add entries to the tar archive
        let mut hdr = tar::Header::new_gnu();
        hdr.set_path("fpk-header.json").unwrap();
        hdr.set_size(header_json.len() as u64);
        hdr.set_mode(0o644);
        hdr.set_cksum();
        archive.append(&hdr, header_json.as_slice()).unwrap();

        let mut payload_hdr = tar::Header::new_gnu();
        payload_hdr.set_path("payload/data.gz").unwrap();
        payload_hdr.set_size(compressed.len() as u64);
        payload_hdr.set_mode(0o644);
        payload_hdr.set_cksum();
        archive.append(&payload_hdr, compressed.as_slice()).unwrap();

        let checksums_content = format!(
            "SHA256(fpk-header.json)= {header_sha256}\nSHA256(payload/data.gz)= {payload_sha256}\n"
        );
        let mut cs_hdr = tar::Header::new_gnu();
        cs_hdr.set_path("checksums.sha256").unwrap();
        cs_hdr.set_size(checksums_content.len() as u64);
        cs_hdr.set_mode(0o644);
        cs_hdr.set_cksum();
        archive
            .append(&cs_hdr, checksums_content.as_bytes())
            .unwrap();

        let sig_data = b"PLACEHOLDER_SIGNATURE";
        let mut sig_hdr = tar::Header::new_gnu();
        sig_hdr.set_path("signature.p7s").unwrap();
        sig_hdr.set_size(sig_data.len() as u64);
        sig_hdr.set_mode(0o644);
        sig_hdr.set_cksum();
        archive.append(&sig_hdr, sig_data.as_slice()).unwrap();

        archive.finish().unwrap();
        (dir, fpk_path)
    }

    /// Create a writer pointed at a temp file for the "device".
    fn writer_for_temp(path: &std::path::Path) -> BlockDeviceWriter {
        let config = FlashConfig::new(path.to_string_lossy().as_ref())
            .sync_after_chunk(false)
            .verify_after_write(false);
        BlockDeviceWriter::new(config)
    }

    #[test]
    fn test_install_full_pipeline() {
        let payload = b"Vela OTA firmware payload for full pipeline test!";
        let (_dir, fpk_path) = build_test_fpk(payload);

        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new(fpk_path.to_string_lossy().as_ref(), writer);

        let bytes = installer.install(None).unwrap();
        assert_eq!(bytes, payload.len() as u64);

        // Verify the file contents
        let written = std::fs::read(device_file.path()).unwrap();
        assert_eq!(&written[..payload.len()], payload);
    }

    #[test]
    fn test_install_with_progress() {
        let payload = b"Progress tracking test payload!";
        let (_dir, fpk_path) = build_test_fpk(payload);

        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new(fpk_path.to_string_lossy().as_ref(), writer);

        let mut calls = Vec::new();
        let cb: ProgressCallback = Box::new(move |w, t| {
            calls.push((w, t));
        });

        let bytes = installer.install(Some(&cb)).unwrap();
        assert_eq!(bytes, payload.len() as u64);
    }

    #[test]
    fn test_convenience_function() {
        let payload = b"Convenience install_fpk test data!";
        let (_dir, fpk_path) = build_test_fpk(payload);

        let device_file = NamedTempFile::new().unwrap();
        let bytes = install_fpk(
            fpk_path.to_str().unwrap(),
            device_file.path().to_str().unwrap(),
            None,
        )
        .unwrap();
        assert_eq!(bytes, payload.len() as u64);
    }

    #[test]
    fn test_corrupt_fpk_rejected() {
        let payload = b"Corrupt test payload...";
        let (_dir, fpk_path) = build_test_fpk(payload);

        // Corrupt the .fpk by truncating it
        let size = std::fs::metadata(&fpk_path).unwrap().len();
        let file = std::fs::OpenOptions::new()
            .write(true)
            .open(&fpk_path)
            .unwrap();
        file.set_len(size / 2).unwrap(); // truncate to half size

        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new(fpk_path.to_string_lossy().as_ref(), writer);

        let result = installer.install(None);
        assert!(result.is_err(), "Corrupt fpk should be rejected");
    }

    #[test]
    fn test_missing_fpk_rejected() {
        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new("/nonexistent/file.fpk", writer);

        let result = installer.install(None);
        assert!(result.is_err(), "Missing fpk should be rejected");
    }

    #[test]
    fn test_empty_payload() {
        let payload: &[u8] = &[];
        let (_dir, fpk_path) = build_test_fpk(payload);

        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new(fpk_path.to_string_lossy().as_ref(), writer);

        let bytes = installer.install(None).unwrap();
        assert_eq!(bytes, 0);
    }

    #[test]
    fn test_large_payload_roundtrip() {
        // 128 KiB of pseudo-random data
        let payload: Vec<u8> = (0..128 * 1024).map(|i| (i % 251) as u8).collect();
        let (_dir, fpk_path) = build_test_fpk(&payload);

        let device_file = NamedTempFile::new().unwrap();
        let writer = writer_for_temp(device_file.path());
        let mut installer = FpkInstaller::new(fpk_path.to_string_lossy().as_ref(), writer);

        let bytes = installer.install(None).unwrap();
        assert_eq!(bytes, payload.len() as u64);

        let written = std::fs::read(device_file.path()).unwrap();
        assert_eq!(&written[..payload.len()], payload.as_slice());
    }
}
