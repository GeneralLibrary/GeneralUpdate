#![forbid(unsafe_code)]
#![doc = "FlashPack (.fpk) update bundle format: parsing, building, validation, and signing."]
#![doc = ""]
#![doc = "## Architecture"]
#![doc = "- `header` — Bundle metadata (`FpkHeader`, `SemVer`, `PayloadType`)"]
#![doc = "- `reader` — Parsing and inspecting .fpk tar archives"]
#![doc = "- `builder` — Constructing .fpk files from payloads"]
#![doc = "- `validator` — Full validation pipeline (checksums + signature + version chain)"]

use thiserror::Error;

pub mod builder;
pub mod header;
pub mod reader;
pub mod validator;

// Re-exports for convenience.
pub use builder::{BuilderConfig, FlashPackBuilder};
pub use header::{FpkHeader, PayloadType, SemVer};
pub use reader::{BundleHash, Checksums, FlashPackReader};
pub use validator::{BundleValidator, sign_bundle};

use vela_crypto::CryptoError;

/// Current reader SemVer constraint, used for `is_reader_compatible` checks.
pub(crate) const REQ_SIZE: &str = "1.0.0";

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
