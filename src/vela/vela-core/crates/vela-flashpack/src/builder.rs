//! FlashPack builder: constructs `.fpk` tar archives from a payload.
//!
//! The builder takes a payload (raw bytes or a file path), bundles it into a
//! tar archive with the required metadata files, and optionally signs it.

use std::fs::{self, File};
use std::io::{BufReader, BufWriter, Read, Write};
use std::path::Path;

use sha2::{Digest, Sha256};
use tracing::{debug, error, info, instrument, trace, warn};

use crate::header::{FpkHeader, PayloadType};
use crate::{FlashPackError, FpkResult};

use vela_crypto::{BundleSigner, sha256};

/// Configuration for building a FlashPack bundle.
#[derive(Debug)]
pub struct BuilderConfig {
    /// Path to the payload file (raw, will be gzipped inside the archive).
    pub payload_path: String,
    /// Bundle name for the header.
    pub bundle_name: String,
    /// Bundle version for the header.
    pub bundle_version: String,
    /// Compatible hardware slots.
    pub compatible_slots: Vec<String>,
    /// Payload classification.
    pub payload_type: PayloadType,
    /// Minimum version required on device before applying.
    pub requires_version: String,
    /// Identifier of the builder.
    pub builder_id: String,
    /// Optional signing key for bundle signature.
    pub signer: Option<Box<dyn BundleSigner>>,
    /// Format version of the produced FlashPack.
    pub format_version: String,
    /// Minimum reader version.
    pub min_reader_version: String,
    /// Compatibility feature flags.
    pub compat_flags: Vec<String>,
}

/// Constructs a `.fpk` file from raw payload data.
pub struct FlashPackBuilder {
    config: BuilderConfig,
}

impl FlashPackBuilder {
    /// Create a new builder with the given configuration.
    pub fn new(config: BuilderConfig) -> Self {
        Self { config }
    }

    /// Build the `.fpk` file and write it to `output_path`.
    ///
    /// This performs the following steps:
    /// 1. Compress payload with gzip.
    /// 2. Compute SHA-256 checksums of all components.
    /// 3. Build the `fpk-header.json`.
    /// 4. Sign the header if a signer is provided.
    /// 5. Assemble the tar archive.
    #[instrument(skip(self), fields(
        bundle = %self.config.bundle_name,
        version = %self.config.bundle_version
    ))]
    pub fn build(&self, output_path: &std::path::Path) -> FpkResult<()> {
        trace!(output = %output_path.display(), "Building FlashPack");

        // 1. Read and compress payload
        let payload_bytes = self.read_payload()?;
        let compressed_payload = self.compress_payload(&payload_bytes)?;

        // 2. Build header
        let header = self.build_header(compressed_payload.len() as u64);
        let header_json = header.to_json()?;

        // 3. Compute checksums
        let header_sha256 = hex::encode(sha256(&header_json)?);
        let payload_sha256 = {
            let mut hasher = Sha256::new();
            hasher.update(&compressed_payload);
            hex::encode(hasher.finalize())
        };
        let checksums_content = format!(
            "SHA256(fpk-header.json)= {header_sha256}\nSHA256(payload/data.gz)= {payload_sha256}\n"
        );

        // 4. Sign the header (sign the header JSON bytes)
        let signature = if let Some(ref signer) = self.config.signer {
            trace!(algorithm = ?signer.algorithm(), "Signing bundle header");
            signer.sign(&header_json)?
        } else {
            warn!("No signer configured — FlashPack will have a placeholder signature");
            b"UNSIGNED".to_vec()
        };

        // 5. Assemble tar archive
        let out_file = File::create(output_path).map_err(FlashPackError::IoError)?;
        let mut archive = tar::Builder::new(BufWriter::new(out_file));

        // fpk-header.json
        let mut hdr = tar::Header::new_gnu();
        hdr.set_path("fpk-header.json")
            .map_err(FlashPackError::IoError)?;
        hdr.set_size(header_json.len() as u64);
        hdr.set_mode(0o644);
        hdr.set_cksum();
        archive
            .append(&hdr, header_json.as_slice())
            .map_err(FlashPackError::IoError)?;
        trace!("Appended fpk-header.json ({} bytes)", header_json.len());

        // payload/data.gz
        let mut payload_hdr = tar::Header::new_gnu();
        payload_hdr
            .set_path("payload/data.gz")
            .map_err(FlashPackError::IoError)?;
        payload_hdr.set_size(compressed_payload.len() as u64);
        payload_hdr.set_mode(0o644);
        payload_hdr.set_cksum();
        archive
            .append(&payload_hdr, compressed_payload.as_slice())
            .map_err(FlashPackError::IoError)?;
        info!(
            size = compressed_payload.len(),
            "Appended compressed payload"
        );

        // checksums.sha256
        let mut cs_hdr = tar::Header::new_gnu();
        cs_hdr
            .set_path("checksums.sha256")
            .map_err(FlashPackError::IoError)?;
        cs_hdr.set_size(checksums_content.len() as u64);
        cs_hdr.set_mode(0o644);
        cs_hdr.set_cksum();
        archive
            .append(&cs_hdr, checksums_content.as_bytes())
            .map_err(FlashPackError::IoError)?;
        trace!("Appended checksums.sha256");

        // signature.p7s
        let mut sig_hdr = tar::Header::new_gnu();
        sig_hdr
            .set_path("signature.p7s")
            .map_err(FlashPackError::IoError)?;
        sig_hdr.set_size(signature.len() as u64);
        sig_hdr.set_mode(0o644);
        sig_hdr.set_cksum();
        archive
            .append(&sig_hdr, signature.as_slice())
            .map_err(FlashPackError::IoError)?;
        trace!("Appended signature.p7s ({} bytes)", signature.len());

        archive.finish().map_err(FlashPackError::IoError)?;

        let output_size = fs::metadata(output_path).map(|m| m.len()).unwrap_or(0);
        info!(
            output = %output_path.display(),
            size = output_size,
            "FlashPack built successfully"
        );
        Ok(())
    }

    /// Read the payload from the configured path.
    fn read_payload(&self) -> FpkResult<Vec<u8>> {
        debug!(path = %self.config.payload_path, "Reading payload");
        fs::read(&self.config.payload_path).map_err(|e| {
            error!(error = %e, path = %self.config.payload_path, "Failed to read payload");
            FlashPackError::IoError(e)
        })
    }

    /// Compress payload data with gzip.
    fn compress_payload(&self, data: &[u8]) -> FpkResult<Vec<u8>> {
        use flate2::Compression;
        use flate2::write::GzEncoder;

        let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
        encoder.write_all(data).map_err(FlashPackError::IoError)?;
        let compressed = encoder.finish().map_err(FlashPackError::IoError)?;

        let ratio = if !data.is_empty() {
            (compressed.len() as f64 / data.len() as f64) * 100.0
        } else {
            100.0
        };
        trace!(
            original = data.len(),
            compressed = compressed.len(),
            ratio_pct = %format!("{ratio:.1}"),
            "Payload compressed"
        );
        Ok(compressed)
    }

    /// Build the FpkHeader for this bundle.
    fn build_header(&self, payload_size: u64) -> FpkHeader {
        FpkHeader {
            format_version: self.config.format_version.clone(),
            min_reader_version: self.config.min_reader_version.clone(),
            bundle_name: self.config.bundle_name.clone(),
            bundle_version: self.config.bundle_version.clone(),
            compatible_slots: self.config.compatible_slots.clone(),
            payload_type: self.config.payload_type,
            payload_size,
            requires_version: self.config.requires_version.clone(),
            created_at: chrono::Utc::now().to_rfc3339(),
            builder_id: self.config.builder_id.clone(),
            compat_flags: self.config.compat_flags.clone(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::header::PayloadType;

    #[test]
    fn test_build_and_read_roundtrip() {
        let dir = tempfile::tempdir().unwrap();
        let payload_path = dir.path().join("payload.bin");
        fs::write(&payload_path, b"test payload for roundtrip validation").unwrap();
        let fpk_path = dir.path().join("test.fpk");

        let config = BuilderConfig {
            payload_path: payload_path.to_string_lossy().to_string(),
            bundle_name: "test-bundle".into(),
            bundle_version: "1.0.0".into(),
            compatible_slots: vec!["test-slot".into()],
            payload_type: PayloadType::Application,
            requires_version: "0.9.0".into(),
            builder_id: "test-ci".into(),
            signer: None,
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            compat_flags: vec![],
        };

        let builder = FlashPackBuilder::new(config);
        builder.build(&fpk_path).unwrap();
        assert!(fpk_path.exists());

        // Roundtrip: read back and verify checksums
        let reader = crate::FlashPackReader::open(&fpk_path).unwrap();
        assert_eq!(reader.header.bundle_name, "test-bundle");
        let hash = reader.verify_checksums().unwrap();
        assert_eq!(hash.sha256.len(), 32);
    }

    #[test]
    fn test_payload_missing() {
        let dir = tempfile::tempdir().unwrap();
        let fpk_path = dir.path().join("test.fpk");
        let config = BuilderConfig {
            payload_path: "/nonexistent/payload.bin".into(),
            bundle_name: "test".into(),
            bundle_version: "1.0.0".into(),
            compatible_slots: vec![],
            payload_type: PayloadType::FullImage,
            requires_version: "0.9.0".into(),
            builder_id: "ci".into(),
            signer: None,
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            compat_flags: vec![],
        };
        let builder = FlashPackBuilder::new(config);
        assert!(builder.build(&fpk_path).is_err());
    }
}
