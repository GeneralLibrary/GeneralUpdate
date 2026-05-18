#![forbid(unsafe_code)]
#![doc = "Vela cryptographic primitives: signing, hashing, and key management."]

use thiserror::Error;
use tracing::instrument;

/// Errors that can occur during cryptographic operations.
#[derive(Error, Debug)]
pub enum CryptoError {
    #[error("Signing failed: {0}")]
    SigningFailed(String),

    #[error("Verification failed: {0}")]
    VerificationFailed(String),

    #[error("Key parsing error: {0}")]
    KeyParsingError(String),

    #[error("Unsupported algorithm: {0}")]
    UnsupportedAlgorithm(String),

    #[error("Hash computation failed: {0}")]
    HashingFailed(String),
}

/// Result type alias for crypto operations.
pub type CryptoResult<T> = Result<T, CryptoError>;

/// Supported signature algorithms.
#[derive(Debug, Clone, Copy, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub enum SignatureAlgorithm {
    RsaPssSha256,
    EcdsaP256Sha256,
}

/// A parsed public key for bundle validation.
#[derive(Debug, Clone)]
pub struct PublicKey {
    pub algorithm: SignatureAlgorithm,
    pub raw: Vec<u8>,
}

/// A parsed private key for bundle signing.
#[derive(Debug)]
pub struct SigningKey {
    pub algorithm: SignatureAlgorithm,
    pub raw: Vec<u8>,
}

/// Trait for signing FlashPack bundles.
pub trait BundleSigner: Send + Sync {
    fn algorithm(&self) -> SignatureAlgorithm;
    fn sign(&self, data: &[u8]) -> CryptoResult<Vec<u8>>;
}

/// Trait for verifying FlashPack signatures.
pub trait BundleVerifier: Send + Sync {
    fn algorithm(&self) -> SignatureAlgorithm;
    fn verify(&self, data: &[u8], signature: &[u8]) -> CryptoResult<bool>;
}

/// Compute SHA-256 hash of data.
#[instrument(skip(data), fields(size = data.len()))]
pub fn sha256(data: &[u8]) -> CryptoResult<Vec<u8>> {
    use sha2::{Digest, Sha256};
    let mut hasher = Sha256::new();
    hasher.update(data);
    Ok(hasher.finalize().to_vec())
}
