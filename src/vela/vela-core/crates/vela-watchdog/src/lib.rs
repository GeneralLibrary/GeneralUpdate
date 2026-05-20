//! Vela Watchdog: hardware watchdog timer integration and system event bus.
//!
//! The watchdog monitors the update process and triggers a hardware reset
//! if the system hangs. The event bus allows decoupled communication between
//! Vela subsystems (lifecycle, slot manager, attestation, etc.).

pub mod bus;
pub mod watchdog;

use thiserror::Error;

/// Errors from watchdog operations.
#[derive(Error, Debug)]
pub enum WatchdogError {
    #[error("Failed to open watchdog device: {0}")]
    OpenFailed(std::io::Error),

    #[error("Failed to pet watchdog: {0}")]
    PetFailed(std::io::Error),

    #[error("Watchdog already armed")]
    AlreadyArmed,

    #[error("Watchdog not armed")]
    NotArmed,

    #[error("Watchdog timed out — system reboot triggered")]
    TimeoutTriggered,

    #[error("Watchdog IO error: {0}")]
    Io(#[from] std::io::Error),
}

/// Result type alias for watchdog operations.
pub type WatchdogResult<T> = Result<T, WatchdogError>;

/// System events broadcast across the Vela subsystems.
///
/// Each variant carries event-specific payload data.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub enum SystemEvent {
    /// A new update is available from the Hub.
    UpdateAvailable {
        rollout_id: String,
        target_version: String,
        flashpack_size: u64,
        force_install: bool,
    },

    /// Update download has started.
    DownloadStarted {
        rollout_id: String,
        total_bytes: u64,
    },

    /// Download progress update.
    DownloadProgress {
        rollout_id: String,
        downloaded_bytes: u64,
        total_bytes: u64,
        percent: f64,
    },

    /// Update download complete.
    DownloadComplete { rollout_id: String },

    /// FlashPack validation started.
    ValidationStarted { rollout_id: String },

    /// FlashPack validation complete.
    ValidationComplete { rollout_id: String, valid: bool },

    /// Installation started.
    InstallStarted {
        rollout_id: String,
        target_slot: String,
    },

    /// Installation complete.
    InstallComplete { rollout_id: String },

    /// System requires reboot to activate the new slot.
    RebootRequired { target_slot: String },

    /// Health heartbeat sent to Hub.
    HealthPulseSent { sequence: u64 },

    /// Watchdog triggered — system reset imminent.
    WatchdogTriggered { last_pet_secs_ago: u64 },

    /// Fallback was activated (booted into the fallback slot).
    FallbackActivated { reason: String },

    /// Attestation completed successfully.
    AttestationComplete { device_id: String },
}

impl SystemEvent {
    /// Human-readable event type name for metrics/logging.
    pub fn event_type(&self) -> &'static str {
        match self {
            Self::UpdateAvailable { .. } => "update_available",
            Self::DownloadStarted { .. } => "download_started",
            Self::DownloadProgress { .. } => "download_progress",
            Self::DownloadComplete { .. } => "download_complete",
            Self::ValidationStarted { .. } => "validation_started",
            Self::ValidationComplete { .. } => "validation_complete",
            Self::InstallStarted { .. } => "install_started",
            Self::InstallComplete { .. } => "install_complete",
            Self::RebootRequired { .. } => "reboot_required",
            Self::HealthPulseSent { .. } => "health_pulse_sent",
            Self::WatchdogTriggered { .. } => "watchdog_triggered",
            Self::FallbackActivated { .. } => "fallback_activated",
            Self::AttestationComplete { .. } => "attestation_complete",
        }
    }
}

impl std::fmt::Display for SystemEvent {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::UpdateAvailable {
                target_version,
                flashpack_size,
                force_install,
                ..
            } => {
                write!(
                    f,
                    "Update available: v{target_version} ({flashpack_size} bytes, force={force_install})"
                )
            }
            Self::DownloadStarted { total_bytes, .. } => {
                write!(f, "Download started: {total_bytes} bytes total")
            }
            Self::DownloadProgress { percent, .. } => {
                write!(f, "Download progress: {percent:.1}%")
            }
            Self::DownloadComplete { .. } => write!(f, "Download complete"),
            Self::ValidationStarted { .. } => write!(f, "Validation started"),
            Self::ValidationComplete { valid, .. } => {
                write!(
                    f,
                    "Validation complete: {}",
                    if *valid { "PASS" } else { "FAIL" }
                )
            }
            Self::InstallStarted { target_slot, .. } => {
                write!(f, "Install started → slot: {target_slot}")
            }
            Self::InstallComplete { .. } => write!(f, "Install complete"),
            Self::RebootRequired { target_slot } => {
                write!(f, "Reboot required → slot: {target_slot}")
            }
            Self::HealthPulseSent { sequence } => {
                write!(f, "Health pulse #{sequence} sent")
            }
            Self::WatchdogTriggered { last_pet_secs_ago } => {
                write!(f, "WATCHDOG TRIGGERED — last pet {last_pet_secs_ago}s ago")
            }
            Self::FallbackActivated { reason } => {
                write!(f, "Fallback activated: {reason}")
            }
            Self::AttestationComplete { device_id } => {
                write!(f, "Attestation complete: {device_id}")
            }
        }
    }
}
