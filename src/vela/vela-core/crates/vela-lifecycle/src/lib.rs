#![forbid(unsafe_code)]
#![doc = "Update lifecycle state machine for Vela OTA."]

use std::collections::HashMap;
use std::sync::Mutex;
use std::time::Duration;
use thiserror::Error;
use tracing::instrument;

/// Errors during the update lifecycle.
#[derive(Error, Debug, Clone)]
pub enum LifecycleError {
    #[error("Phase timeout: {0:?}")]
    PhaseTimeout(UpdatePhase),

    #[error("Invalid state transition from {from:?} to {to:?}")]
    InvalidTransition { from: UpdatePhase, to: UpdatePhase },

    #[error("Hook execution failed in phase {phase:?}: {message}")]
    HookError { phase: UpdatePhase, message: String },

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

/// Possible outcomes of an update lifecycle.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LifecycleOutcome {
    Success,
    FallbackRecovery { reason: String, phase: UpdatePhase },
    Aborted,
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
}

/// Hook trait for injecting custom logic per phase.
#[async_trait::async_trait]
pub trait PhaseHook: Send + Sync {
    fn phase(&self) -> UpdatePhase;
    async fn on_enter(&self, _ctx: &LifecycleContext) -> LifecycleResult<()> { Ok(()) }
    async fn on_exit(&self, _ctx: &LifecycleContext) -> LifecycleResult<()> { Ok(()) }
    async fn on_error(&self, _ctx: &LifecycleContext, _err: &LifecycleError) -> LifecycleResult<()> { Ok(()) }
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
        Self { phase, start: std::time::Instant::now() }
    }

    pub fn complete(self, _ctx: &LifecycleContext) {
        let elapsed = self.start.elapsed().as_millis() as u64;
        tracing::info!(phase = ?self.phase, elapsed_ms = elapsed, "Phase completed");
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

/// The lifecycle engine drives phase transitions.
pub struct LifecycleEngine {
    pub config: LifecycleConfig,
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

impl LifecycleEngine {
    pub fn new(config: LifecycleConfig) -> Self {
        Self { config }
    }

    pub fn phase_timeout(&self, phase: UpdatePhase) -> Duration {
        self.config.phase_timeouts.get(&phase).copied().unwrap_or(Duration::from_secs(600))
    }
}
