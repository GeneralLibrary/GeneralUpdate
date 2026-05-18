//! Hub HTTP client for Vela OTA — poll, download, heartbeat, attest.
//!
//! All endpoints are authenticated via session token obtained through
//! device attestation. Includes exponential backoff retry for transient
//! failures and range-based download resume support.

pub mod client;
pub mod download;
pub mod retry;

use thiserror::Error;

/// Errors from Hub HTTP communication.
#[derive(Error, Debug)]
pub enum HubError {
    #[error("HTTP client error: {0}")]
    Http(#[from] reqwest::Error),

    #[error("Invalid HTTP status {0}: {1}")]
    HttpStatus(u16, String),

    #[error("Hub URL is not configured")]
    NotConfigured,

    #[error("Session token missing or expired — re-attestation required")]
    AuthRequired,

    #[error("Hub returned rate-limit: retry after {0:?}")]
    RateLimited(std::time::Duration),

    #[error("JSON deserialization error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("Download interrupted at byte {0}, expected {1}")]
    DownloadInterrupted(u64, u64),

    #[error("Checksum mismatch: expected {expected}, got {actual}")]
    ChecksumMismatch { expected: String, actual: String },

    #[error("Invalid URL: {0}")]
    InvalidUrl(String),
}

/// Result type alias for Hub operations.
pub type HubResult<T> = Result<T, HubError>;

/// Configuration for connecting to the Vela Hub.
#[derive(Debug, Clone)]
pub struct HubConfig {
    /// Base URL of the Vela Hub (e.g., "https://hub.vela-ota.dev").
    pub base_url: String,
    /// Session token from device attestation.
    pub auth_token: Option<String>,
    /// TLS client certificate path (for mTLS).
    pub tls_client_cert: Option<String>,
    /// TLS client key path.
    pub tls_client_key: Option<String>,
    /// CA certificate bundle path.
    pub tls_ca_cert: Option<String>,
}

impl HubConfig {
    /// Create a config with just the base URL.
    pub fn new(base_url: impl Into<String>) -> Self {
        Self {
            base_url: base_url.into(),
            auth_token: None,
            tls_client_cert: None,
            tls_client_key: None,
            tls_ca_cert: None,
        }
    }

    /// Set the auth token.
    pub fn with_auth(mut self, token: impl Into<String>) -> Self {
        self.auth_token = Some(token.into());
        self
    }

    /// Join a path segment to the base URL.
    pub fn url(&self, path: &str) -> String {
        format!("{}{}", self.base_url.trim_end_matches('/'), path)
    }
}

/// Outcome of polling the Hub for updates.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub enum PollOutcome {
    /// A new update is available.
    UpdateAvailable(RolloutManifest),
    /// No update available at this time.
    NoUpdate,
    /// The Hub instructed us to retry later.
    RetryLater { retry_after_secs: u64 },
}

/// Manifest for an available update, returned by the Hub on poll.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct RolloutManifest {
    pub rollout_id: String,
    pub flashpack_url: String,
    pub flashpack_checksum: String,
    pub flashpack_size: u64,
    pub target_version: String,
    pub force_install: bool,
    pub deadline: Option<String>,
    pub release_notes: Option<String>,
}
