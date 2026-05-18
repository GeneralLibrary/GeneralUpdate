#![forbid(unsafe_code)]
#![doc = "FlashPack (.fpk) update bundle format parsing, building, and validation."]

use thiserror::Error;
use tracing::instrument;
use vela_crypto::CryptoError;

/// Errors specific to FlashPack operations.
#[derive(Error, Debug)]
pub enum FlashPackError {
    #[error("Invalid FlashPack format: {0}")]
    InvalidFormat(String),

    #[error("Checksum mismatch: expected {expected}, got {actual}")]
    ChecksumMismatch { expected: String, actual: String },

    #[error("Version requirement not met: current {current}, required {required}")]
    VersionTooLow { current: String, required: String },

    #[error("Format version {format_version} is not compatible (min reader: {min_reader})")]
    FormatIncompatible {
        format_version: String,
        min_reader: String,
    },

    #[error("IO error: {0}")]
    IoError(#[from] std::io::Error),

    #[error("Crypto error: {0}")]
    Crypto(#[from] CryptoError),

    #[error("JSON parse error: {0}")]
    JsonError(#[from] serde_json::Error),
}

/// Result type alias for FlashPack operations.
pub type FpkResult<T> = Result<T, FlashPackError>;

/// Payload type classification.
#[derive(Debug, Clone, Copy, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum PayloadType {
    FullImage,
    Delta,
    Application,
}

/// FlashPack bundle header metadata.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct FpkHeader {
    pub format_version: String,
    pub min_reader_version: String,
    pub bundle_name: String,
    pub bundle_version: String,
    pub compatible_slots: Vec<String>,
    pub payload_type: PayloadType,
    pub payload_size: u64,
    pub requires_version: String,
    pub created_at: String,
    pub builder_id: String,
    pub compat_flags: Vec<String>,
}

/// Parsed FlashPack bundle ready for validation.
#[derive(Debug)]
pub struct FlashPackReader {
    pub header: FpkHeader,
    pub checksums: Vec<u8>,
    pub signature: Vec<u8>,
    pub payload_path: String,
}

/// Result of bundle validation.
#[derive(Debug, Clone)]
pub struct BundleHash {
    pub sha256: [u8; 32],
}

impl FlashPackReader {
    /// Open and parse a .fpk file.
    #[instrument(fields(path = %path))]
    pub fn open(path: &str) -> FpkResult<Self> {
        tracing::trace!("Opening FlashPack file");
        Err(FlashPackError::InvalidFormat(
            "FlashPackReader::open not yet implemented".into(),
        ))
    }
}
