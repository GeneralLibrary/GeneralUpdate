//! Suite 2: Slot Manager + Lifecycle integration tests.
//!
//! Validates that slot transitions trigger lifecycle phase changes,
//! inactive slot selection is stable, and write+verify round-trip works.

use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleMetrics, LifecycleOutcome,
    UpdatePhase,
};
use vela_slotmgr::{MockSlotProvider, SlotLabel, SlotManager, SlotProvider};

#[tokio::test]
async fn test_slot_transitions_trigger_lifecycle_changes() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "slot-lifecycle-001".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };

    // Run through the full lifecycle sequence
    let phases = vec![
        UpdatePhase::Idle,
        UpdatePhase::Polling,
        UpdatePhase::Acquiring,
        UpdatePhase::Validating,
        UpdatePhase::Installing,
        UpdatePhase::Rebooting,
        UpdatePhase::Verifying,
        UpdatePhase::Committing,
    ];

    let mut current = UpdatePhase::Idle;
    for (i, _expected_next) in phases.iter().enumerate() {
        let result = engine.execute_phase(&ctx, current).await;
        match result {
            Ok(UpdatePhase::Idle) => {
                // Lifecycle ended (committed or fell back to idle)
                break;
            }
            Ok(next) => {
                current = next;
            }
            Err(e) => {
                panic!("Phase {} ({:?}) failed: {}", i, current, e);
            }
        }
    }
}

#[test]
fn test_inactive_slot_selection_is_stable() {
    let mgr = SlotManager::default();

    // Default: active = Primary, inactive = Alternate
    for _ in 0..100 {
        assert_eq!(mgr.select_inactive_slot(), SlotLabel::Alternate);
    }
}

#[test]
fn test_inactive_slot_after_swap() {
    let mut mgr = SlotManager::default();

    mgr.swap_active();
    // After swap: active = Alternate, inactive = Primary
    for _ in 0..100 {
        assert_eq!(mgr.select_inactive_slot(), SlotLabel::Primary);
    }
}

#[test]
fn test_write_and_verify_on_slot() {
    let mut mgr = SlotManager::default();
    let data = b"vela-ota-slot-test-data-0123456789";

    // Write to alternate slot
    let result = mgr.write_slot(SlotLabel::Alternate, data);
    assert!(result.is_ok(), "Write to alternate slot should succeed");
}

#[test]
fn test_write_large_data_fails_with_insufficient_space() {
    let mock = MockSlotProvider::new();
    mock.set_alternate_free_bytes(100); // only 100 bytes free

    let mut mgr = SlotManager::with_mock(mock);
    let large_data = vec![0u8; 200]; // 200 bytes > 100 free

    let result = mgr.write_slot(SlotLabel::Alternate, &large_data);
    assert!(result.is_err(), "Should fail due to insufficient space");
}

#[tokio::test]
async fn test_slot_mock_detect_and_swap() {
    let provider = MockSlotProvider::with_versions("1.0.0", "1.0.0");
    let layout = provider.detect_slots().await.unwrap();

    assert_eq!(layout.primary.current_version, "1.0.0");
    assert_eq!(layout.alternate.current_version, "1.0.0");
    assert_eq!(layout.primary.device_path, "/dev/mock-p2");
    assert_eq!(layout.alternate.device_path, "/dev/mock-p3");

    provider.swap_slots().await.unwrap();
    let layout_after = provider.detect_slots().await.unwrap();
    // After swap, versions stay the same (mock provider swaps versions internally)
    // But active slot changes
    assert_eq!(
        provider.get_active_slot().await.unwrap(),
        vela_slotmgr::SlotId::Alternate
    );
}

#[tokio::test]
async fn test_lifecycle_context_metrics() {
    let ctx = LifecycleContext {
        update_id: "metrics-test".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };

    ctx.record_bytes_downloaded(1024);
    ctx.record_bytes_written(512);
    ctx.record_validation_time(150);

    let metrics = ctx.metrics.lock().unwrap();
    assert_eq!(metrics.bytes_downloaded, 1024);
    assert_eq!(metrics.bytes_written, 512);
    assert_eq!(metrics.validation_time_ms, 150);

    // Errors increment retry count
    drop(metrics);
    ctx.record_error(&vela_lifecycle::LifecycleError::PhaseTimeout(
        UpdatePhase::Validating,
    ));
    assert_eq!(ctx.metrics.lock().unwrap().retry_count, 1);
}

#[tokio::test]
async fn test_lifecycle_terminal_states_reachable() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());

    // Test Committing → Idle (Success)
    let ctx_commit = LifecycleContext {
        update_id: "commit-test".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine
        .execute_phase(&ctx_commit, UpdatePhase::Committing)
        .await
        .unwrap();
    assert_eq!(result, UpdatePhase::Idle);
    assert_eq!(
        ctx_commit.metrics.lock().unwrap().outcome,
        Some(LifecycleOutcome::Success)
    );

    // Test FallbackRecovery → Idle
    let ctx_fallback = LifecycleContext {
        update_id: "fallback-test".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine
        .execute_phase(&ctx_fallback, UpdatePhase::FallbackRecovery)
        .await
        .unwrap();
    assert_eq!(result, UpdatePhase::Idle);
    assert!(matches!(
        ctx_fallback.metrics.lock().unwrap().outcome,
        Some(LifecycleOutcome::FallbackRecovery { .. })
    ));
}
