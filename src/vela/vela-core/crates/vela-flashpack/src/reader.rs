//! FlashPack reader: parse and inspect `.fpk` tar archives.
//!
//! The reader opens a `.fpk` file, extracts `fpk-header.json`, verifies the
//! internal structure, and makes the payload accessible for streaming.
//!
//! # Format
//! ```text
//! .fpk (tar)
//! ├── fpk-header.json
//! ├── payload/
//! │   ├── data.gz
//! │   └── delta.manifest    (optional)
//! ├── checksums.sha256
//! └── signature.p7s
//! ```

use std::fs::File;
use std::io::{BufReader, Read, Seek, SeekFrom};

use tracing::{debug, error, info, instrument, trace, warn};
use vela_crypto::sha256;

use crate::header::FpkHeader;
use crate::header::PayloadType;
use crate::{FlashPackError, FpkResult, REQ_SIZE};
use sha2::{Digest, Sha256};

/// Result of a SHA-256 checksum verification.
#[derive(Debug, Clone)]
pub struct BundleHash {
    /// The computed SHA-256 hash of the payload.
    pub sha256: [u8; 32],
}

/// Component checksums stored in `checksums.sha256`.
#[derive(Debug, Clone, Default)]
pub struct Checksums {
    /// Hash of `fpk-header.json`.
    pub header_sha256: String,
    /// Hash of `payload/data.gz` (or the main payload).
    pub payload_sha256: String,
    /// Hash of `payload/delta.manifest` if present.
    pub delta_manifest_sha256: Option<String>,
}

/// A parsed FlashPack bundle ready for validation and installation.
pub struct FlashPackReader {
    /// Parsed bundle header.
    pub header: FpkHeader,
    /// Decoded checksums from the archive.
    pub checksums: Checksums,
    /// Raw PKCS#7 detached signature bytes.
    pub signature: Vec<u8>,
    /// Path to the `.fpk` file on disk (for streaming reads).
    archive_path: String,
    /// Tar entry offset for `payload/data.gz` (for streaming).
    payload_offset: u64,
    /// Tar entry size for `payload/data.gz`.
    payload_entry_size: u64,
}

impl std::fmt::Debug for FlashPackReader {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("FlashPackReader")
            .field("header", &self.header)
            .field("checksums", &self.checksums)
            .field("signature.len", &self.signature.len())
            .field("archive_path", &self.archive_path)
            .finish_non_exhaustive()
    }
}

impl FlashPackReader {
    /// Open and parse a `.fpk` file.
    ///
    /// This reads the tar header entries, extracts `fpk-header.json`,
    /// `checksums.sha256`, and `signature.p7s`, and records the offset
    /// of the payload for later streaming reads.
    #[instrument(fields(path = %path.display()))]
    pub fn open(path: &std::path::Path) -> FpkResult<Self> {
        trace!("Opening FlashPack file");

        let file = File::open(path).map_err(|e| {
            error!(error = %e, "Failed to open FlashPack file");
            FlashPackError::IoError(e)
        })?;

        let file_size = file.metadata().map(|m| m.len()).unwrap_or(0);
        trace!(file_size, "FlashPack file opened");

        let _archive = tar::Archive::new(BufReader::new(file));
        let mut header: Option<FpkHeader> = None;
        let mut checksums: Option<Checksums> = None;
        let mut signature: Option<Vec<u8>> = None;
        let mut payload_offset: Option<u64> = None;
        let mut payload_entry_size: Option<u64> = None;
        let mut _has_payload_dir = false;
        let mut has_payload_data = false;

        // First pass: read all entries to locate metadata and record payload offset.
        // We must re-open the file for each streaming operation later.
        let file2 = File::open(path).map_err(FlashPackError::IoError)?;
        let mut archive2 = tar::Archive::new(BufReader::new(file2));

        for entry_result in archive2.entries().map_err(FlashPackError::IoError)? {
            let mut entry = entry_result.map_err(FlashPackError::IoError)?;
            let entry_path = entry.path().map_err(FlashPackError::IoError)?;
            let path_str = entry_path.to_string_lossy().to_string();

            match path_str.as_str() {
                "fpk-header.json" => {
                    let mut buf = Vec::new();
                    entry
                        .read_to_end(&mut buf)
                        .map_err(FlashPackError::IoError)?;
                    header = Some(FpkHeader::from_json(&buf)?);
                    trace!(bundle = %header.as_ref().unwrap().bundle_name, "Parsed FlashPack header");
                }
                "checksums.sha256" => {
                    let mut buf = String::new();
                    entry
                        .read_to_string(&mut buf)
                        .map_err(FlashPackError::IoError)?;
                    checksums = Some(Self::parse_checksums(&buf)?);
                }
                "signature.p7s" => {
                    let mut buf = Vec::new();
                    entry
                        .read_to_end(&mut buf)
                        .map_err(FlashPackError::IoError)?;
                    signature = Some(buf);
                }
                "payload/" => {
                    _has_payload_dir = true;
                }
                "payload/data.gz" => {
                    has_payload_data = true;
                    payload_offset = Some(entry.raw_file_position());
                    payload_entry_size = Some(entry.size());
                    trace!(
                        offset = payload_offset,
                        size = payload_entry_size,
                        "Located payload entry"
                    );
                }
                other => {
                    debug!(entry = %other, "Ignoring unknown tar entry");
                }
            }
        }

        // All required entries must be present.
        let header = header.ok_or_else(|| {
            error!("Missing fpk-header.json in FlashPack archive");
            FlashPackError::InvalidFormat("Missing fpk-header.json".into())
        })?;

        let checksums = checksums.ok_or_else(|| {
            error!("Missing checksums.sha256 in FlashPack archive");
            FlashPackError::InvalidFormat("Missing checksums.sha256".into())
        })?;

        let signature = signature.ok_or_else(|| {
            error!("Missing signature.p7s in FlashPack archive");
            FlashPackError::InvalidFormat("Missing signature.p7s".into())
        })?;

        if !has_payload_data {
            return Err(FlashPackError::InvalidFormat(
                "Missing payload/data.gz in FlashPack archive".into(),
            ));
        }

        let payload_offset = payload_offset.unwrap_or(0);
        let payload_entry_size = payload_entry_size.unwrap_or(0);

        // Validate payload size matches header if header specifies it.
        if header.payload_size > 0 && payload_entry_size != header.payload_size {
            warn!(
                expected = header.payload_size,
                actual = payload_entry_size,
                "Payload size mismatch"
            );
            return Err(FlashPackError::InvalidFormat(format!(
                "Payload size mismatch: header says {}, archive has {}",
                header.payload_size, payload_entry_size
            )));
        }

        // Check min_reader_version compatibility using the current reader version.
        let reader_version: crate::header::SemVer = REQ_SIZE.parse().unwrap();
        if !header.is_reader_compatible(&reader_version) {
            return Err(FlashPackError::FormatIncompatible {
                format_version: header.format_version.clone(),
                min_reader: header.min_reader_version.clone(),
            });
        }

        info!(
            bundle = %header.bundle_name,
            version = %header.bundle_version,
            format = %header.format_version,
            payload_type = ?header.payload_type,
            "FlashPack opened successfully"
        );

        Ok(Self {
            header,
            checksums,
            signature,
            archive_path: path.to_string_lossy().to_string(),
            payload_offset,
            payload_entry_size,
        })
    }

    /// Open a streaming reader over the payload data.
    ///
    /// Returns a `BufReader` positioned at the start of `payload/data.gz`.
    /// The caller is responsible for decompressing (gzip) if needed.
    pub fn payload_reader(&self) -> FpkResult<impl Read> {
        let mut file = File::open(&self.archive_path).map_err(FlashPackError::IoError)?;
        file.seek(SeekFrom::Start(self.payload_offset))
            .map_err(FlashPackError::IoError)?;
        Ok(BufReader::new(file).take(self.payload_entry_size))
    }

    /// Verify the SHA-256 checksums of all components against the recorded values.
    #[instrument(skip(self))]
    pub fn verify_checksums(&self) -> FpkResult<BundleHash> {
        debug!("Starting checksum verification");

        // Re-read the archive to compute hashes of each component.
        let file = File::open(&self.archive_path).map_err(FlashPackError::IoError)?;
        let mut archive = tar::Archive::new(BufReader::new(file));

        let mut header_bytes = Vec::new();
        let mut payload_hash = None;

        for entry_result in archive.entries().map_err(FlashPackError::IoError)? {
            let mut entry = entry_result.map_err(FlashPackError::IoError)?;
            let entry_path = entry.path().map_err(FlashPackError::IoError)?;

            match entry_path.to_string_lossy().as_ref() {
                "fpk-header.json" => {
                    entry
                        .read_to_end(&mut header_bytes)
                        .map_err(FlashPackError::IoError)?;
                }
                "payload/data.gz" => {
                    let mut hasher = Sha256::new();
                    let mut buf = [0u8; 8192];
                    loop {
                        let n = entry.read(&mut buf).map_err(FlashPackError::IoError)?;
                        if n == 0 {
                            break;
                        }
                        hasher.update(&buf[..n]);
                    }
                    payload_hash = Some(hasher.finalize());
                }
                _ => {}
            }
        }

        // Verify header checksum
        let computed_header = hex::encode(sha256(&header_bytes)?);
        if computed_header != self.checksums.header_sha256 {
            error!(
                expected = %self.checksums.header_sha256,
                actual = %computed_header,
                "Header checksum mismatch"
            );
            return Err(FlashPackError::ChecksumMismatch {
                expected: self.checksums.header_sha256.clone(),
                actual: computed_header,
            });
        }

        // Verify payload checksum
        let payload_hash = payload_hash.ok_or_else(|| {
            FlashPackError::InvalidFormat(
                "Could not find payload/data.gz for checksum verification".into(),
            )
        })?;
        let computed_payload = hex::encode(payload_hash);
        if computed_payload != self.checksums.payload_sha256 {
            error!(
                expected = %self.checksums.payload_sha256,
                actual = %computed_payload,
                "Payload checksum mismatch"
            );
            return Err(FlashPackError::ChecksumMismatch {
                expected: self.checksums.payload_sha256.clone(),
                actual: computed_payload,
            });
        }

        let mut hash_bytes = [0u8; 32];
        hash_bytes.copy_from_slice(&payload_hash);

        info!(
            hash = %hex::encode(&hash_bytes[..8]),
            "Checksum verification passed"
        );
        Ok(BundleHash { sha256: hash_bytes })
    }

    /// Parse the `checksums.sha256` text file.
    ///
    /// Expected format (one per line):
    /// ```text
    /// SHA256(fpk-header.json)= abcdef...
    /// SHA256(payload/data.gz)= 123456...
    /// SHA256(payload/delta.manifest)= fedcba...   (optional)
    /// ```
    fn parse_checksums(content: &str) -> FpkResult<Checksums> {
        let mut checksums = Checksums::default();
        for line in content.lines() {
            let line = line.trim();
            if line.is_empty() || line.starts_with('#') {
                continue;
            }
            let (file_part, hash_part) = line.split_once('=').ok_or_else(|| {
                FlashPackError::InvalidFormat(format!("Invalid checksum line: {line}"))
            })?;

            let file_path = file_part
                .trim()
                .strip_prefix("SHA256(")
                .and_then(|s| s.strip_suffix(')'))
                .ok_or_else(|| {
                    FlashPackError::InvalidFormat(format!(
                        "Invalid checksum file path format: {file_part}"
                    ))
                })?;

            let hash = hash_part.trim().to_string();

            match file_path {
                "fpk-header.json" => checksums.header_sha256 = hash,
                "payload/data.gz" => checksums.payload_sha256 = hash,
                "payload/delta.manifest" => checksums.delta_manifest_sha256 = Some(hash),
                other => {
                    warn!(file = %other, "Unknown entry in checksums file, ignoring");
                }
            }
        }

        if checksums.header_sha256.is_empty() {
            return Err(FlashPackError::InvalidFormat(
                "checksums.sha256 missing header entry".into(),
            ));
        }
        if checksums.payload_sha256.is_empty() {
            return Err(FlashPackError::InvalidFormat(
                "checksums.sha256 missing payload entry".into(),
            ));
        }

        Ok(checksums)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    /// Build a minimal valid `.fpk` file in a temp directory for testing.
    fn build_test_fpk(header: &FpkHeader) -> (tempfile::TempDir, std::path::PathBuf) {
        let dir = tempfile::tempdir().unwrap();
        let fpk_path = dir.path().join("test.fpk");

        let file = std::fs::File::create(&fpk_path).unwrap();
        let mut archive = tar::Builder::new(file);

        // 1. fpk-header.json
        let header_json = serde_json::to_vec_pretty(header).unwrap();
        let header_sha256 = hex::encode(sha256(&header_json).unwrap());
        let mut header_entry = tar::Header::new_gnu();
        header_entry.set_path("fpk-header.json").unwrap();
        header_entry.set_size(header_json.len() as u64);
        header_entry.set_mode(0o644);
        header_entry.set_cksum();
        archive
            .append(&header_entry, header_json.as_slice())
            .unwrap();

        // 2. payload/data.gz (just some bytes)
        let payload_data = b"This is test payload data for FlashPack validation";
        let payload_sha256_hex = hex::encode(sha256(payload_data).unwrap());
        let mut payload_entry = tar::Header::new_gnu();
        payload_entry.set_path("payload/data.gz").unwrap();
        payload_entry.set_size(payload_data.len() as u64);
        payload_entry.set_mode(0o644);
        payload_entry.set_cksum();
        archive
            .append(&payload_entry, payload_data.as_slice())
            .unwrap();

        // 3. checksums.sha256
        let checksums_content = format!(
            "SHA256(fpk-header.json)= {header_sha256}\nSHA256(payload/data.gz)= {payload_sha256_hex}\n"
        );
        let mut checksums_entry = tar::Header::new_gnu();
        checksums_entry.set_path("checksums.sha256").unwrap();
        checksums_entry.set_size(checksums_content.len() as u64);
        checksums_entry.set_mode(0o644);
        checksums_entry.set_cksum();
        archive
            .append(&checksums_entry, checksums_content.as_bytes())
            .unwrap();

        // 4. signature.p7s (placeholder)
        let sig_data = b"PLACEHOLDER_SIGNATURE_BYTES";
        let mut sig_entry = tar::Header::new_gnu();
        sig_entry.set_path("signature.p7s").unwrap();
        sig_entry.set_size(sig_data.len() as u64);
        sig_entry.set_mode(0o644);
        sig_entry.set_cksum();
        archive.append(&sig_entry, sig_data.as_slice()).unwrap();

        archive.finish().unwrap();
        (dir, fpk_path)
    }

    fn sample_header() -> FpkHeader {
        FpkHeader {
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            bundle_name: "vela-os-v2.1.3".into(),
            bundle_version: "2.1.3".into(),
            compatible_slots: vec!["rpi4-model-b".into()],
            payload_type: PayloadType::FullImage,
            payload_size: 50, // length of test payload
            requires_version: "2.0.0".into(),
            created_at: "2026-05-18T12:00:00Z".into(),
            builder_id: "ci/v0.1".into(),
            compat_flags: vec![],
        }
    }

    #[test]
    fn test_open_valid_fpk() {
        let header = sample_header();
        let (_dir, fpk_path) = build_test_fpk(&header);

        let reader = FlashPackReader::open(&fpk_path).unwrap();
        assert_eq!(reader.header.bundle_name, "vela-os-v2.1.3");
        assert_eq!(reader.header.bundle_version, "2.1.3");
        assert!(!reader.checksums.header_sha256.is_empty());
        assert!(!reader.checksums.payload_sha256.is_empty());
        assert!(!reader.signature.is_empty());
    }

    #[test]
    fn test_verify_checksums_valid() {
        let header = sample_header();
        let (_dir, fpk_path) = build_test_fpk(&header);

        let reader = FlashPackReader::open(&fpk_path).unwrap();
        let hash = reader.verify_checksums().unwrap();
        assert_eq!(hash.sha256.len(), 32);
    }

    #[test]
    fn test_checksum_mismatch_detected() {
        let header = sample_header();
        let (_dir, fpk_path) = build_test_fpk(&header);

        let mut reader = FlashPackReader::open(&fpk_path).unwrap();
        // Tamper with the recorded checksum
        reader.checksums.payload_sha256 =
            "0000000000000000000000000000000000000000000000000000000000000000".to_string();
        let result = reader.verify_checksums();
        assert!(result.is_err());
    }

    #[test]
    fn test_format_incompatible_rejected() {
        let header = sample_header();
        let mut incompatible = header.clone();
        incompatible.format_version = "99.0.0".into();
        incompatible.min_reader_version = "99.0.0".into();
        let (_dir, fpk_path) = build_test_fpk(&incompatible);

        let result = FlashPackReader::open(&fpk_path);
        assert!(result.is_err());
    }

    #[test]
    fn test_payload_reader_streaming() {
        let header = sample_header();
        let (_dir, fpk_path) = build_test_fpk(&header);

        let reader = FlashPackReader::open(&fpk_path).unwrap();
        let mut payload = reader.payload_reader().unwrap();
        let mut buf = Vec::new();
        payload.read_to_end(&mut buf).unwrap();
        let expected = b"This is test payload data for FlashPack validation";
        assert_eq!(buf, expected);
    }
}
