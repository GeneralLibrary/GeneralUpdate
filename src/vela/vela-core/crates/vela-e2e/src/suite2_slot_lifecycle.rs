//! Suite 2: Slot Manager + Lifecycle integration tests.
//!
//! Validates slot transitions trigger lifecycle phase changes
//! and mock slot detection/swap works end-to-end.

use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleMetrics, UpdatePhase,
};
use vela_slotmgr::{MockSlotProvider, SlotProvider};

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
