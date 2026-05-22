#![forbid(unsafe_code)]
#![doc = "Update lifecycle state machine for Vela OTA."]
#![doc = ""]
#![doc = "Manages the full OTA update flow from Idle through Commit"]
#![doc = "with per-phase timeout enforcement and structured observability."]

use std::collections::HashMap;
use std::sync::Mutex;
use std::time::Duration;
use thiserror::Error;
use tracing::instrument;

pub mod engine;

pub use engine::{LifecycleEngine, run_lifecycle};

/// Errors during the update lifecycle.
#[derive(Error, Debug)]
pub enum LifecycleError {
    #[error("Phase timeout: {0:?}")]
    PhaseTimeout(UpdatePhase),

    #[error("Invalid state transition from {from:?} to {to:?}")]
    InvalidTransition { from: UpdatePhase, to: UpdatePhase },

    #[error("Hook execution failed in phase {phase:?}: {message}")]
    HookError { phase: UpdatePhase, message: String },

    #[error("FPK not available: no FlashPack path configured")]
    FpkNotAvailable,

    #[error("No target device configured")]
    NoTargetDevice,

    #[error("Install error: {0}")]
    InstallError(String),

    #[error("Slot error: {0}")]
    SlotError(String),

    #[error("Operation aborted")]
    Aborted,
}

/// Result type alias for lifecycle operations.
pub type LifecycleResult<T> = Result<T, LifecycleError>;

/// Update lifecycle phases (strict state machine).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, serde::Serialize, serde::Deserialize)]
pub enum UpdatePhase {
    Idle,
    Polling,
    Acquiring,
    Validating,
    Installing,
    Rebooting,
    Verifying,
    Committing,
    FallbackRecovery,
}

impl std::fmt::Display for UpdatePhase {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Idle => write!(f, "Idle"),
            Self::Polling => write!(f, "Polling"),
            Self::Acquiring => write!(f, "Acquiring"),
            Self::Validating => write!(f, "Validating"),
            Self::Installing => write!(f, "Installing"),
            Self::Rebooting => write!(f, "Rebooting"),
            Self::Verifying => write!(f, "Verifying"),
            Self::Committing => write!(f, "Committing"),
            Self::FallbackRecovery => write!(f, "FallbackRecovery"),
        }
    }
}

/// Possible outcomes of an update lifecycle.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LifecycleOutcome {
    Success,
    FallbackRecovery { reason: String, phase: UpdatePhase },
    Aborted,
}

/// Configuration for the lifecycle engine.
#[derive(Debug, Clone)]
pub struct LifecycleConfig {
    pub phase_timeouts: HashMap<UpdatePhase, Duration>,
}

impl Default for LifecycleConfig {
    fn default() -> Self {
        let mut phase_timeouts = HashMap::new();
        phase_timeouts.insert(UpdatePhase::Polling, Duration::from_secs(30));
        phase_timeouts.insert(UpdatePhase::Acquiring, Duration::from_secs(3600));
        phase_timeouts.insert(UpdatePhase::Validating, Duration::from_secs(120));
        phase_timeouts.insert(UpdatePhase::Installing, Duration::from_secs(1800));
        phase_timeouts.insert(UpdatePhase::Rebooting, Duration::from_secs(300));
        phase_timeouts.insert(UpdatePhase::Verifying, Duration::from_secs(120));
        Self { phase_timeouts }
    }
}

/// Observable metrics collected during an update.
#[derive(Debug, Clone, Default)]
pub struct LifecycleMetrics {
    pub phase_durations: HashMap<UpdatePhase, u64>,
    pub phase_attempts: HashMap<UpdatePhase, u32>,
    pub retry_count: u32,
    pub total_elapsed_ms: u64,
    pub bytes_downloaded: u64,
    pub bytes_written: u64,
    pub validation_time_ms: u64,
    pub outcome: Option<LifecycleOutcome>,
}

/// Shared context carried through all phases.
pub struct LifecycleContext {
    pub update_id: String,
    pub metrics: Mutex<LifecycleMetrics>,
    /// Path to the downloaded `.fpk` file.
    pub fpk_path: Mutex<Option<String>>,
    /// Target block device for flashing (e.g., `/dev/mmcblk0p3`).
    pub target_device: Mutex<Option<String>>,
    /// Expected SHA-256 checksum of the decompressed payload.
    pub expected_checksum: Mutex<Option<String>>,
    /// Target version for this update.
    pub target_version: Mutex<Option<String>>,
}

impl LifecycleContext {
    /// Create a new context with the given update ID.
    pub fn new(update_id: impl Into<String>) -> Self {
        Self {
            update_id: update_id.into(),
            metrics: Mutex::new(LifecycleMetrics::default()),
            fpk_path: Mutex::new(None),
            target_device: Mutex::new(None),
            expected_checksum: Mutex::new(None),
            target_version: Mutex::new(None),
        }
    }

    /// Record an error that occurred during a phase.
    pub fn record_error(&self, _err: &LifecycleError) {
        if let Ok(mut m) = self.metrics.lock() {
            m.retry_count += 1;
        }
    }

    /// Record bytes downloaded during the Acquiring phase.
    pub fn record_bytes_downloaded(&self, bytes: u64) {
        if let Ok(mut m) = self.metrics.lock() {
            m.bytes_downloaded += bytes;
        }
    }

    /// Record bytes written during the Installing phase.
    pub fn record_bytes_written(&self, bytes: u64) {
        if let Ok(mut m) = self.metrics.lock() {
            m.bytes_written += bytes;
        }
    }

    /// Record time spent in signature / bundle validation.
    pub fn record_validation_time(&self, ms: u64) {
        if let Ok(mut m) = self.metrics.lock() {
            m.validation_time_ms = ms;
        }
    }
}

/// Hook trait for injecting custom logic per phase.
#[async_trait::async_trait]
pub trait PhaseHook: Send + Sync {
    fn phase(&self) -> UpdatePhase;
    async fn on_enter(&self, _ctx: &LifecycleContext) -> LifecycleResult<()> {
        Ok(())
    }
    async fn on_exit(&self, _ctx: &LifecycleContext) -> LifecycleResult<()> {
        Ok(())
    }
    async fn on_error(
        &self,
        _ctx: &LifecycleContext,
        _err: &LifecycleError,
    ) -> LifecycleResult<()> {
        Ok(())
    }
}

/// RAII timer for tracking phase durations.
pub struct PhaseTimer {
    phase: UpdatePhase,
    start: std::time::Instant,
}

impl PhaseTimer {
    #[instrument(skip_all, fields(phase = ?phase))]
    pub fn begin(phase: UpdatePhase, _ctx: &LifecycleContext) -> Self {
        tracing::info!(?phase, "Entering phase");
        Self {
            phase,
            start: std::time::Instant::now(),
        }
    }

    pub fn complete(self, ctx: &LifecycleContext) {
        let elapsed = self.start.elapsed().as_millis() as u64;
        tracing::info!(phase = ?self.phase, elapsed_ms = elapsed, "Phase completed");
        if let Ok(mut m) = ctx.metrics.lock() {
            m.phase_durations.insert(self.phase, elapsed);
            m.total_elapsed_ms += elapsed;
        }
    }
}

/// Hub-issued update instruction.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct RolloutManifest {
    pub rollout_id: String,
    pub flashpack_url: String,
    pub flashpack_checksum: String,
    pub target_version: String,
    pub force_install: bool,
    pub deadline: Option<String>,
}
