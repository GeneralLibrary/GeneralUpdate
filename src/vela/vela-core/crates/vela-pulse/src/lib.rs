#![forbid(unsafe_code)]
#![doc = "Health pulse: periodic status reporting from device to Vela Hub."]

use thiserror::Error;
use tracing::instrument;

/// Errors during health pulse operations.
#[derive(Error, Debug)]
pub enum PulseError {
    #[error("Network error: {0}")]
    NetworkError(String),

    #[error("Hub rejected pulse (status {0}): {1}")]
    Rejected(u16, String),

    #[error("Serialization error: {0}")]
    SerializationError(#[from] serde_json::Error),

    #[error("Max retries exceeded ({attempts} attempts)")]
    MaxRetriesExceeded { attempts: u32 },
}

/// Result type alias for pulse operations.
pub type PulseResult<T> = Result<T, PulseError>;

/// Retry configuration with exponential backoff.
#[derive(Debug, Clone)]
pub struct RetryConfig {
    pub max_retries: u32,
    pub initial_delay: std::time::Duration,
    pub max_delay: std::time::Duration,
}

impl Default for RetryConfig {
    fn default() -> Self {
        Self {
            max_retries: 5,
            initial_delay: std::time::Duration::from_secs(1),
            max_delay: std::time::Duration::from_secs(60),
        }
    }
}

/// Health pulse report sent from device to Hub.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct HealthPulseReport {
    pub sequence: u64,
    pub timestamp: String,
    pub device_serial: String,
    pub current_version: String,
    pub lifecycle_phase: String,
    pub cpu_percent: f32,
    pub memory_used_mb: u64,
    pub disk_free_mb: u64,
    pub temperature_celsius: Option<f32>,
    pub last_update_result: Option<String>,
    pub network_connected: bool,
}

/// Send a single health pulse to the Hub.
#[instrument(skip_all)]
pub async fn send_pulse_single(_report: &HealthPulseReport) -> PulseResult<()> {
    tracing::trace!("Sending health pulse");
    // TODO: HTTP POST to hub
    Ok(())
}
