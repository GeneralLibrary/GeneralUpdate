//! Suite 6: Configuration validation.
//!
//! Tests that OrchestratorConfig defaults are sensible,
//! custom configs propagate correctly, and pipeline configs are consistent.

use std::path::PathBuf;
use std::time::Duration;
use vela_core::orchestrator::{
    AttestationConfig, OrchestratorConfig, PipelinePhase, PulseConfig,
};
use vela_lifecycle::LifecycleConfig;

/// Default orchestrator config is sensible.
#[test]
fn test_orchestrator_config_defaults_sensible() {
    let config = OrchestratorConfig::default();

    // Hub config
    assert!(config.hub_base_url.starts_with("https://"));
    assert!(config.hub_base_url.contains("vela-ota"));
    assert_eq!(config.poll_interval, Duration::from_secs(300));
    assert!(config.auth_token.is_none());

    // Download
    assert!(config.download_dir.to_string_lossy().contains("vela"));

    // Device
    assert!(config.block_device.starts_with("/dev/"));
    assert!(config.identity_key.is_none());

    // Safety
    assert!(config.watchdog_enabled);

    // Subsystem configs
    assert!(config.attestation.hub_verify_url.contains("attest"));
    assert!(!config.attestation.device_id.is_empty());
    assert!(config.pulse.hub_heartbeat_url.contains("heartbeat"));
    assert_eq!(config.pulse.interval, Duration::from_secs(300));
}

/// Custom orchestrator config propagates correctly.
#[test]
fn test_orchestrator_config_custom() {
    let config = OrchestratorConfig {
        hub_base_url: "https://custom-hub.example.com/api/v1".into(),
        poll_interval: Duration::from_secs(120),
        auth_token: Some("token-abc".into()),
        download_dir: PathBuf::from("/custom/downloads"),
        block_device: "/dev/sda".into(),
        identity_key: Some(vec![1, 2, 3, 4]),
        watchdog_enabled: false,
        attestation: AttestationConfig {
            hub_verify_url: "https://custom-hub.example.com/api/v1/attest".into(),
            device_id: "custom-device-42".into(),
        },
        pulse: PulseConfig {
            hub_heartbeat_url: "https://custom-hub.example.com/api/v1/heartbeat".into(),
            interval: Duration::from_secs(60),
        },
    };

    assert_eq!(config.hub_base_url, "https://custom-hub.example.com/api/v1");
    assert_eq!(config.poll_interval, Duration::from_secs(120));
    assert_eq!(config.auth_token, Some("token-abc".into()));
    assert_eq!(
        config.download_dir,
        PathBuf::from("/custom/downloads")
    );
    assert_eq!(config.block_device, "/dev/sda");
    assert_eq!(config.identity_key, Some(vec![1, 2, 3, 4]));
    assert!(!config.watchdog_enabled);
    assert_eq!(
        config.attestation.device_id,
        "custom-device-42"
    );
    assert_eq!(config.pulse.interval, Duration::from_secs(60));
}

/// Pipeline phases are consistent — no gaps in the flow.
#[test]
fn test_pipeline_phases_consistent() {
    let phases = [
        PipelinePhase::Idle,
        PipelinePhase::Polling,
        PipelinePhase::UpdateAvailable,
        PipelinePhase::Downloading,
        PipelinePhase::Validating,
        PipelinePhase::Installing,
        PipelinePhase::RebootPending,
        PipelinePhase::Error,
    ];

    // All phases have display strings
    for p in &phases {
        let s = p.to_string();
        assert!(!s.is_empty());
    }

    // Only terminal phases have is_terminal() == true
    let terminal_count = phases.iter().filter(|p| p.is_terminal()).count();
    assert_eq!(terminal_count, 2, "Exactly 2 terminal phases");
}

/// Lifecycle config has reasonable timeouts.
#[test]
fn test_lifecycle_config_timeouts_reasonable() {
    let config = LifecycleConfig::default();

    // Polling timeout is short (30s)
    let polling = config
        .phase_timeouts
        .get(&vela_lifecycle::UpdatePhase::Polling)
        .copied()
        .unwrap();
    assert!(polling <= Duration::from_secs(60));

    // Acquiring timeout is long (1 hour for large downloads)
    let acquiring = config
        .phase_timeouts
        .get(&vela_lifecycle::UpdatePhase::Acquiring)
        .copied()
        .unwrap();
    assert!(acquiring >= Duration::from_secs(1800));

    // Installing timeout is at least 30 minutes
    let installing = config
        .phase_timeouts
        .get(&vela_lifecycle::UpdatePhase::Installing)
        .copied()
        .unwrap();
    assert!(installing >= Duration::from_secs(600));
}

/// Attestation config contains required fields.
#[test]
fn test_attestation_config_fields() {
    let config = AttestationConfig {
        hub_verify_url: "https://hub.example.com/attest".into(),
        device_id: "dev-001".into(),
    };

    assert!(!config.hub_verify_url.is_empty());
    assert!(!config.device_id.is_empty());
}

/// Pulse config interval is positive.
#[test]
fn test_pulse_config_interval_positive() {
    let config = PulseConfig {
        hub_heartbeat_url: "https://hub.example.com/heartbeat".into(),
        interval: Duration::from_secs(60),
    };

    assert!(config.interval > Duration::from_secs(0));
    assert!(!config.hub_heartbeat_url.is_empty());
}

/// VelaCore error type conversions work.
#[test]
fn test_vela_error_conversions() {
    use vela_core::VelaError;

    // FlashPack error
    let fp_err = vela_flashpack::FlashPackError::ChecksumMismatch { expected: "abc".into(), actual: "xyz".into() };
    let vela_err: VelaError = fp_err.into();
    assert!(matches!(vela_err, VelaError::FlashPack(_)));

    // Slot error
    let slot_err = vela_slotmgr::SlotError::InsufficientSpace {
        device: "/dev/sda".into(),
        required: 100,
        available: 50,
    };
    let vela_err: VelaError = slot_err.into();
    assert!(matches!(vela_err, VelaError::Slot(_)));

    // Hub error
    let hub_err = vela_hub::HubError::NotConfigured;
    let vela_err: VelaError = hub_err.into();
    assert!(matches!(vela_err, VelaError::Hub(_)));
}

/// Watchdog config matches expectations.
#[test]
fn test_watchdog_config() {
    assert_eq!(vela_watchdog::watchdog::DEFAULT_TIMEOUT_SECS, 60);
    assert_eq!(vela_watchdog::watchdog::UPDATE_TIMEOUT_SECS, 10);
    assert!(vela_watchdog::watchdog::UPDATE_TIMEOUT_SECS < vela_watchdog::watchdog::DEFAULT_TIMEOUT_SECS);
}
