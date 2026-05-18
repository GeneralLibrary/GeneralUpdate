#![forbid(unsafe_code)]
#![doc = "Device attestation: identity proof, health pulse, and session token management."]

pub mod attester;
pub mod identity;
pub mod pulse;

use thiserror::Error;
use tracing::instrument;

/// Errors during attestation.
#[derive(Error, Debug)]
pub enum AttestationError {
    #[error("Device identity not configured")]
    IdentityNotConfigured,

    #[error("Attestation challenge failed: {0}")]
    ChallengeFailed(String),

    #[error("Token expired")]
    TokenExpired,

    #[error("Network error: {0}")]
    NetworkError(String),

    #[error("Invalid response from Hub: {0}")]
    InvalidResponse(String),
}

/// Result type alias for attestation operations.
pub type AttestationResult<T> = Result<T, AttestationError>;

/// Unique device identity, burned at factory.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct DeviceIdentity {
    pub serial: String,
    pub hardware_fingerprint: String,
    pub model: String,
    pub manufacturer: String,
}

/// Challenge from Hub for anti-replay.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct AttestationChallenge {
    pub nonce: String,
    pub expires_at: String,
}

/// Short-lived JWT session token.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct SessionToken {
    pub jwt_token: String,
    pub expires_at: String,
    pub refresh_token: String,
}

impl SessionToken {
    /// Check if token expires within the given duration.
    pub fn is_expiring_soon(&self, _within: std::time::Duration) -> bool {
        false
    }
}

/// Initiate device attestation with the Vela Hub.
#[instrument(skip(identity))]
pub async fn request_attestation(
    identity: &DeviceIdentity,
    _hub_url: &str,
) -> AttestationResult<SessionToken> {
    tracing::debug!(serial = %identity.serial, "Initiating device attestation");
    Err(AttestationError::IdentityNotConfigured)
}
