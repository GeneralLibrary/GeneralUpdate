//! Update orchestration engine — the central coordinator that ties the
//! Vela OTA subsystems together into a single cohesive update pipeline.
//!
//! ## Architecture
//!
//! The orchestrator owns all subsystem handles and drives the update
//! lifecycle through a typed state machine:
//!
//! ```text
//! Idle → Poll → Acquire → Download → Validate → Install → RebootRequired
//! ```
//!
//! At each transition the orchestrator:
//! - Emits a `SystemEvent` on the event bus
//! - Updates the lifecycle state machine
//! - Arms/pets/disarms the hardware watchdog during critical sections

use std::path::PathBuf;
use std::sync::Arc;
use std::time::Duration;
use tokio::sync::watch;
use tracing::{error, info, instrument, warn};

use vela_attestation::{AttestationResult, AttestationStatus};
use vela_flashpack::FlashPackError;
use vela_hub::RetryStrategy;
use vela_lifecycle::{LifecycleEngine, Phase};
use vela_slotmgr::{SlotLabel, SlotManager};
use vela_watchdog::{
    SystemEvent, SystemEventBus, Watchdog,
};
use vela_watchdog::bus::Subscriber;

use crate::{VelaError, VelaResult};

// ─── configuration ─────────────────────────────────────────────

/// Configuration for the update orchestration pipeline.
#[derive(Debug, Clone)]
pub struct OrchestratorConfig {
    /// Hub server base URL.
    pub hub_base_url: String,
    /// Hub poll interval.
    pub poll_interval: Duration,
    /// Auth token for Hub API calls.
    pub auth_token: Option<String>,
    /// Directory for downloaded FlashPack artifacts.
    pub download_dir: PathBuf,
    /// Root device path for slot management (e.g. `/dev/mmcblk0`).
    pub block_device: String,
    /// SHA-256 of the identity signing key for attestation.
    pub identity_key: Option<Vec<u8>>,
    /// Whether to enable the hardware watchdog.
    pub watchdog_enabled: bool,

    // Subsystem configs
    /// Attestation config.
    pub attestation: AttestationConfig,
    /// Health pulse config.
    pub pulse: PulseConfig,
}

/// Attestation subsystem configuration.
#[derive(Debug, Clone)]
pub struct AttestationConfig {
    /// URL for attestation verification endpoint.
    pub hub_verify_url: String,
    /// Device identity string.
    pub device_id: String,
}

/// Health pulse subsystem configuration.
#[derive(Debug, Clone)]
pub struct PulseConfig {
    /// Hub heartbeat endpoint URL.
    pub hub_heartbeat_url: String,
    /// Pulse interval.
    pub interval: Duration,
}

impl Default for OrchestratorConfig {
    fn default() -> Self {
        Self {
            hub_base_url: "https://hub.vela-ota.dev/api/v1".into(),
            poll_interval: Duration::from_secs(300),
            auth_token: None,
            download_dir: PathBuf::from("/var/cache/vela/downloads"),
            block_device: "/dev/mmcblk0".into(),
            identity_key: None,
            watchdog_enabled: true,
            attestation: AttestationConfig {
                hub_verify_url: "https://hub.vela-ota.dev/api/v1/attest".into(),
                device_id: "vela-device-00".into(),
            },
            pulse: PulseConfig {
                hub_heartbeat_url: "https://hub.vela-ota.dev/api/v1/heartbeat".into(),
                interval: Duration::from_secs(300),
            },
        }
    }
}

// ─── pipeline phases ───────────────────────────────────────────

/// The update pipeline as a typed state machine.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PipelinePhase {
    /// Idle — waiting for the next poll cycle.
    Idle,
    /// Polling the Hub for updates.
    Polling,
    /// An update is available, preparing to acquire.
    UpdateAvailable,
    /// Downloading the FlashPack artifact.
    Downloading,
    /// Validating the FlashPack signature and checksum.
    Validating,
    /// Installing to the target slot.
    Installing,
    /// Awaiting system reboot to activate the new slot.
    RebootPending,
    /// Error state — pipeline halted, requires intervention.
    Error,
}

impl PipelinePhase {
    pub fn is_terminal(&self) -> bool {
        matches!(self, Self::RebootPending | Self::Error)
    }
}

impl std::fmt::Display for PipelinePhase {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let s = match self {
            Self::Idle => "Idle",
            Self::Polling => "Polling",
            Self::UpdateAvailable => "UpdateAvailable",
            Self::Downloading => "Downloading",
            Self::Validating => "Validating",
            Self::Installing => "Installing",
            Self::RebootPending => "RebootPending",
            Self::Error => "Error",
        };
        write!(f, "{s}")
    }
}

// ─── pipeline result ───────────────────────────────────────────

/// Outcome of a pipeline run.
#[derive(Debug)]
pub enum PipelineOutcome {
    /// Update found and staged, reboot required.
    UpdateStaged {
        rollout_id: String,
        target_version: String,
        target_slot: String,
    },
    /// No update available this cycle.
    NoUpdate,
    /// Pipeline halted with error.
    Error(VelaError),
}

// ─── orchestrator ──────────────────────────────────────────────

/// Central orchestrator that coordinates the full Vela OTA update pipeline.
///
/// Owns handles to all subsystems and exposes a single entry point:
/// `run_once()` for one-shot operation or `run_loop()` for daemon mode.
pub struct UpdateOrchestrator {
    config: OrchestratorConfig,
    hub: vela_hub::VelaHubClient,
    lifecycle: LifecycleEngine,
    slot_mgr: SlotManager,
    watchdog: Option<Watchdog>,
    event_bus: SystemEventBus,
}

impl UpdateOrchestrator {
    /// Build an orchestrator from configuration.
    ///
    /// Initializes all subsystem handles but does not start any
    /// background tasks — that happens in `run_once()` / `run_loop()`.
    #[instrument(skip(config))]
    pub fn new(config: OrchestratorConfig) -> VelaResult<Self> {
        let hub_config = vela_hub::HubConfig {
            base_url: config.hub_base_url.clone(),
            auth_token: config.auth_token.clone(),
            timeout_secs: 60,
            max_retries: 3,
        };
        let hub = vela_hub::VelaHubClient::new(hub_config)
            .map_err(|e| VelaError::Hub(e))?;

        let lifecycle = LifecycleEngine::default();
        let slot_mgr = SlotManager::default();
        let event_bus = SystemEventBus::default();

        let watchdog = if config.watchdog_enabled {
            match Watchdog::open() {
                Ok(wd) => {
                    info!("Watchdog available — update safety enabled");
                    Some(wd)
                }
                Err(e) => {
                    warn!(%e, "Watchdog not available — continuing without hardware safety net");
                    None
                }
            }
        } else {
            None
        };

        Ok(Self {
            config,
            hub,
            lifecycle,
            slot_mgr,
            watchdog,
            event_bus,
        })
    }

    /// Get a reference to the event bus (for external subscribers).
    pub fn event_bus(&self) -> &SystemEventBus {
        &self.event_bus
    }

    /// Get a subscriber for system events.
    pub fn subscribe(&self) -> Subscriber {
        self.event_bus.subscribe()
    }

    /// Subscribe with history replay.
    pub fn subscribe_with_history(&self) -> (Vec<SystemEvent>, Subscriber) {
        self.event_bus.subscribe_with_history()
    }

    // ─── main entry points ─────────────────────────────────────

    /// Run one full update pipeline cycle.
    ///
    /// Returns the outcome for caller inspection. Does not block
    /// on reboot — returns `RebootPending` when ready for reboot.
    #[instrument(skip(self))]
    pub async fn run_once(&mut self) -> PipelineOutcome {
        info!("Starting update pipeline cycle");

        // ── Phase: Poll ───────────────────────────────────────
        self.emit(PipelinePhase::Polling);
        self.lifecycle.transition_to(Phase::Acquiring);

        let poll = match self.hub.poll_for_update().await {
            Ok(outcome) => outcome,
            Err(e) => {
                error!(%e, "Hub poll failed");
                self.lifecycle.transition_to(Phase::Idle);
                return PipelineOutcome::Error(VelaError::Hub(e));
            }
        };

        let manifest = match poll {
            vela_hub::PollOutcome::UpdateAvailable(m) => {
                info!(
                    version = %m.target_version,
                    rollout_id = %m.rollout_id,
                    size = m.flashpack_size,
                    "Update available from Hub"
                );
                self.emit_event(SystemEvent::UpdateAvailable {
                    rollout_id: m.rollout_id.clone(),
                    target_version: m.target_version.clone(),
                    flashpack_size: m.flashpack_size,
                    force_install: m.force_install,
                });
                m
            }
            vela_hub::PollOutcome::NoUpdate => {
                info!("No update available");
                self.lifecycle.transition_to(Phase::Idle);
                return PipelineOutcome::NoUpdate;
            }
            vela_hub::PollOutcome::RetryLater(dur) => {
                info!(retry_in_ms = dur.as_millis(), "Hub requested retry later");
                self.lifecycle.transition_to(Phase::Idle);
                return PipelineOutcome::NoUpdate;
            }
        };

        // ── Phase: Download ─────────────────────────────────────
        self.emit(PipelinePhase::Downloading);
        let dest_path = self
            .config
            .download_dir
            .join(format!("{}.fpk", manifest.rollout_id));

        // Arm watchdog for critical section
        let watchdog_guard = self.arm_watchdog_for_update();

        self.emit_event(SystemEvent::DownloadStarted {
            rollout_id: manifest.rollout_id.clone(),
            total_bytes: manifest.flashpack_size,
        });

        let retry = RetryStrategy::for_download();
        let hub_config_for_dl = vela_hub::HubConfig {
            base_url: self.config.hub_base_url.clone(),
            auth_token: self.config.auth_token.clone(),
            timeout_secs: 300,
            max_retries: 5,
        };

        if let Err(e) = vela_hub::download_with_retry(
            &hub_config_for_dl,
            &manifest.flashpack_url,
            manifest.flashpack_size,
            Some(&manifest.flashpack_checksum),
            dest_path.clone(),
            &retry,
        )
        .await
        {
            error!(%e, "Download failed");
            return self.error_exit(e.into(), watchdog_guard);
        }

        self.emit_event(SystemEvent::DownloadComplete {
            rollout_id: manifest.rollout_id.clone(),
        });

        // Pet watchdog after download
        if let Some(ref mut guard) = watchdog_guard {
            let _ = guard.pet();
        }

        // ── Phase: Validate ─────────────────────────────────────
        self.emit(PipelinePhase::Validating);
        self.emit_event(SystemEvent::ValidationStarted {
            rollout_id: manifest.rollout_id.clone(),
        });

        // Validate FlashPack bundle
        match validate_flashpack(&dest_path).await {
            Ok(true) => {
                self.emit_event(SystemEvent::ValidationComplete {
                    rollout_id: manifest.rollout_id.clone(),
                    valid: true,
                });
            }
            Ok(false) => {
                warn!("FlashPack validation failed — corrupt or tampered");
                let _ = tokio::fs::remove_file(&dest_path).await;
                return self.error_exit(
                    VelaError::Orchestrator("FlashPack validation failed".into()),
                    watchdog_guard,
                );
            }
            Err(e) => {
                return self.error_exit(e, watchdog_guard);
            }
        }

        // ── Phase: Install ─────────────────────────────────────
        self.emit(PipelinePhase::Installing);

        // Select the non-active slot
        let target_slot = self.slot_mgr.select_inactive_slot();
        let slot_name = target_slot.label().to_string();

        self.emit_event(SystemEvent::InstallStarted {
            rollout_id: manifest.rollout_id.clone(),
            target_slot: slot_name.clone(),
        });

        // Extract and install
        if let Err(e) = self.install_to_slot(&dest_path, target_slot).await {
            return self.error_exit(e, watchdog_guard);
        }

        self.emit_event(SystemEvent::InstallComplete {
            rollout_id: manifest.rollout_id.clone(),
        });

        // Pet watchdog — we're through the danger zone
        if let Some(ref mut guard) = watchdog_guard {
            let _ = guard.pet();
        }

        // ── Phase: Attest ─────────────────────────────────────
        info!("Attesting new slot integrity");
        let attest_result = self.attest_slot(target_slot).await;
        match attest_result {
            Ok(AttestationStatus::Passed) => {
                self.emit_event(SystemEvent::AttestationComplete {
                    device_id: self.config.attestation.device_id.clone(),
                });
            }
            Ok(AttestationStatus::Failed(reason)) => {
                return self.error_exit(
                    VelaError::Orchestrator(format!("Slot attestation failed: {reason}")),
                    watchdog_guard,
                );
            }
            Err(e) => {
                return self.error_exit(e.into(), watchdog_guard);
            }
        }

        // ── Phase: Ready for reboot ────────────────────────────
        self.lifecycle.transition_to(Phase::Rebooting);
        self.emit_event(SystemEvent::RebootRequired {
            target_slot: slot_name.clone(),
        });

        // Disarm watchdog before controlled reboot
        if let Some(guard) = watchdog_guard {
            let _ = guard.disarm();
        }

        info!(
            rollout_id = %manifest.rollout_id,
            target_version = %manifest.target_version,
            target_slot = %slot_name,
            "Update staged — reboot required"
        );

        PipelineOutcome::UpdateStaged {
            rollout_id: manifest.rollout_id,
            target_version: manifest.target_version,
            target_slot: slot_name,
        }
    }

    /// Run the orchestrator in daemon mode — poll/update loop forever.
    #[instrument(skip(self))]
    pub async fn run_loop(&mut self) -> VelaResult<()> {
        info!(
            poll_interval_ms = self.config.poll_interval.as_millis(),
            "Starting Vela OTA daemon loop"
        );

        loop {
            match self.run_once().await {
                PipelineOutcome::UpdateStaged { .. } => {
                    info!("Update staged — exiting loop for reboot");
                    return Ok(());
                }
                PipelineOutcome::NoUpdate => {
                    tokio::time::sleep(self.config.poll_interval).await;
                }
                PipelineOutcome::Error(e) => {
                    error!(%e, "Pipeline error — backing off before retry");
                    tokio::time::sleep(Duration::from_secs(60)).await;
                }
            }
        }
    }

    // ─── internal helpers ──────────────────────────────────────

    fn emit(&self, phase: PipelinePhase) {
        info!(%phase, "Pipeline phase transition");
    }

    fn emit_event(&self, event: SystemEvent) {
        self.event_bus.publish(event);
    }

    fn arm_watchdog_for_update(&mut self) -> Option<vela_watchdog::WatchdogGuard<'_>> {
        match self.watchdog.as_mut() {
            Some(wd) => match wd.arm_for_update() {
                Ok(guard) => {
                    info!("Watchdog armed for update (10s timeout)");
                    Some(guard)
                }
                Err(e) => {
                    warn!(%e, "Failed to arm watchdog");
                    None
                }
            },
            None => None,
        }
    }

    fn error_exit(
        &mut self,
        error: VelaError,
        _guard: Option<vela_watchdog::WatchdogGuard<'_>>,
    ) -> PipelineOutcome {
        self.lifecycle.transition_to(Phase::Idle);
        // Guard will drop — if watchdog is armed, it will fire (intentional)
        error!(%error, "Pipeline error — returning to Idle");
        PipelineOutcome::Error(error)
    }

    async fn install_to_slot(
        &mut self,
        fpk_path: &std::path::Path,
        slot: SlotLabel,
    ) -> VelaResult<()> {
        // Read and extract FlashPack to the target slot partition
        let data = tokio::fs::read(fpk_path).await.map_err(|e| {
            VelaError::Orchestrator(format!("Failed to read FlashPack for install: {e}"))
        })?;

        self.slot_mgr
            .write_slot(slot, &data)
            .map_err(|e| VelaError::Slot(e))?;

        info!(slot = %slot.label(), "Installed to slot");
        Ok(())
    }

    async fn attest_slot(
        &self,
        slot: SlotLabel,
    ) -> Result<AttestationStatus, vela_attestation::AttestationError> {
        let provider = vela_attestation::DefaultMeasurementProvider::new(
            self.config.block_device.clone(),
            slot.label().to_string(),
        );

        let attestation = vela_attestation::attest(&provider, &self.config.attestation.device_id)
            .map_err(|e| {
                error!(%e, "Slot attestation failed");
                e
            })?;

        match attestation {
            AttestationResult::Passed => Ok(AttestationStatus::Passed),
            AttestationResult::Failed(reason) => {
                warn!(%reason, "Slot attestation FAILED");
                Ok(AttestationStatus::Failed(reason))
            }
        }
    }
}

// ─── FlashPack validation helper ───────────────────────────────

async fn validate_flashpack(path: &std::path::Path) -> VelaResult<bool> {
    let data = tokio::fs::read(path).await.map_err(|e| {
        VelaError::Orchestrator(format!("Failed to read FlashPack for validation: {e}"))
    })?;

    // Delegate to FlashPack reader for structural validation
    match vela_flashpack::FlashPackReader::open(&data) {
        Ok(reader) => {
            // Verify signature via crypto layer
            match reader.verify() {
                Ok(()) => {
                    info!("FlashPack validated: structural + cryptographic OK");
                    Ok(true)
                }
                Err(FlashPackError::SignatureMismatch) => {
                    warn!("FlashPack signature mismatch — tampered?");
                    Ok(false)
                }
                Err(e) => {
                    Err(VelaError::FlashPack(e))
                }
            }
        }
        Err(e) => {
            warn!(%e, "FlashPack structural validation failed");
            Ok(false)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_pipeline_phase_display() {
        assert_eq!(PipelinePhase::Idle.to_string(), "Idle");
        assert_eq!(PipelinePhase::Polling.to_string(), "Polling");
        assert_eq!(PipelinePhase::Installing.to_string(), "Installing");
        assert_eq!(PipelinePhase::RebootPending.to_string(), "RebootPending");
        assert_eq!(PipelinePhase::Error.to_string(), "Error");
    }

    #[test]
    fn test_pipeline_phase_is_terminal() {
        assert!(!PipelinePhase::Idle.is_terminal());
        assert!(!PipelinePhase::Polling.is_terminal());
        assert!(!PipelinePhase::Downloading.is_terminal());
        assert!(PipelinePhase::RebootPending.is_terminal());
        assert!(PipelinePhase::Error.is_terminal());
    }

    #[test]
    fn test_orchestrator_config_defaults() {
        let config = OrchestratorConfig::default();
        assert!(config.hub_base_url.contains("vela-ota.dev"));
        assert!(config.watchdog_enabled);
        assert_eq!(config.poll_interval, Duration::from_secs(300));
        assert_eq!(config.pulse.interval, Duration::from_secs(300));
    }

    #[tokio::test]
    async fn test_event_bus_integration() {
        let bus = SystemEventBus::new(16);
        let mut sub = bus.subscribe();

        bus.publish(SystemEvent::UpdateAvailable {
            rollout_id: "test-r1".into(),
            target_version: "1.2.3".into(),
            flashpack_size: 2048,
            force_install: false,
        });

        let event = tokio::time::timeout(Duration::from_millis(100), sub.recv())
            .await
            .unwrap()
            .unwrap();

        assert_eq!(event.event_type(), "update_available");
    }

    #[test]
    fn test_orchestrator_config_custom() {
        let config = OrchestratorConfig {
            hub_base_url: "https://custom.example.com".into(),
            poll_interval: Duration::from_secs(60),
            auth_token: Some("token-abc".into()),
            ..Default::default()
        };
        assert_eq!(config.hub_base_url, "https://custom.example.com");
        assert_eq!(config.poll_interval, Duration::from_secs(60));
        assert_eq!(config.auth_token, Some("token-abc".into()));
    }
}
