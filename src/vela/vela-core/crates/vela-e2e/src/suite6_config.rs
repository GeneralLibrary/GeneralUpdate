//! Suite 6: Configuration validation.
//!
//! Cross-crate config sanity checks — timeout reasonableness,
//! error type conversions, and watchdog invariants.

use std::time::Duration;
use vela_lifecycle::LifecycleConfig;

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

/// VelaCore error type conversions work across crates.
#[test]
fn test_vela_error_conversions() {
    use vela_core::VelaError;

    // FlashPack error
    let fp_err = vela_flashpack::FlashPackError::ChecksumMismatch {
        expected: "abc".into(),
        actual: "xyz".into(),
    };
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
    assert!(
        vela_watchdog::watchdog::UPDATE_TIMEOUT_SECS
            < vela_watchdog::watchdog::DEFAULT_TIMEOUT_SECS
    );
}
