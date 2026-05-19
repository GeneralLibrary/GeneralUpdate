//! Vela Delta — binary delta engine for efficient incremental OTA updates.
//!
//! Generates compact binary patches between two firmware versions
//! using a sliding-window block-matching algorithm. Only changed
//! bytes are transmitted, dramatically reducing download size for
//! point-release updates.
//!
//! ## Format
//!
//! The delta format is a simple binary patch format:
//!
//! ```text
//! [magic: 4 bytes "VDLT"]
//! [base_hash: 32 bytes SHA-256 of base file]
//! [target_hash: 32 bytes SHA-256 of target file]
//! [instruction_count: 4 bytes LE u32]
//! [instructions...]
//! ```
//!
//! Each instruction is either:
//! - `COPY(offset, length)` — copy `length` bytes from base at `offset`
//! - `INSERT(length, data...)` — insert `length` bytes of new data

use thiserror::Error;

pub mod diff;
pub mod manifest;
pub mod patch;

pub(crate) use diff::Instruction;

pub use diff::generate_delta;
pub use manifest::DeltaManifest;
pub use patch::apply_patch;

/// Magic bytes identifying a Vela Delta patch file.
pub const DELTA_MAGIC: &[u8; 4] = b"VDLT";

/// Maximum window size for the block-matching algorithm.
pub const MAX_WINDOW_SIZE: usize = 64 * 1024; // 64 KiB

/// Minimum match length to emit a COPY instruction.
pub const MIN_MATCH_LEN: usize = 8;

/// Errors from delta operations.
#[derive(Error, Debug)]
pub enum DeltaError {
    #[error("Invalid delta format: {0}")]
    InvalidFormat(String),

    #[error("Base file hash mismatch: expected {expected}, got {actual}")]
    BaseHashMismatch { expected: String, actual: String },

    #[error("Target hash mismatch after patching: expected {expected}, got {actual}")]
    TargetHashMismatch { expected: String, actual: String },

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Delta too large: {size} bytes exceeds {limit}")]
    TooLarge { size: usize, limit: usize },
}

/// Result type alias for delta operations.
pub type DeltaResult<T> = Result<T, DeltaError>;

/// Compute SHA-256 hash of data.
pub fn hash(data: &[u8]) -> String {
    use sha2::Digest;
    hex::encode(sha2::Sha256::digest(data))
}
