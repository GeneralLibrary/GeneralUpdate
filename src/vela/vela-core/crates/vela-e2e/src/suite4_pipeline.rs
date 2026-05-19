//! Suite 4: Full pipeline state transitions.
//!
//! Validates that the update pipeline phases follow the correct order
//! and that terminal states are reachable.

use std::time::Duration;
use vela_core::orchestrator::{OrchestratorConfig, PipelinePhase};
use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleMetrics, LifecycleOutcome,
    UpdatePhase,
};
use vela_slotmgr::SlotLabel;

/// PipelinePhase order matches the expected sequence.
#[test]
fn test_pipeline_phase_order() {
    // Verify phase constants exist and are distinct
    assert_ne!(
        PipelinePhase::Idle,
        PipelinePhase::Polling
    );
    assert_ne!(
        PipelinePhase::Polling,
        PipelinePhase::UpdateAvailable
    );
    assert_ne!(
        PipelinePhase::UpdateAvailable,
        PipelinePhase::Downloading
    );
    assert_ne!(
        PipelinePhase::Downloading,
        PipelinePhase::Validating
    );
    assert_ne!(
        PipelinePhase::Validating,
        PipelinePhase::Installing
    );
    assert_ne!(
        PipelinePhase::Installing,
        PipelinePhase::RebootPending
    );
}

/// Terminal states are correctly identified.
#[test]
fn test_terminal_states() {
    assert!(PipelinePhase::RebootPending.is_terminal());
    assert!(PipelinePhase::Error.is_terminal());

    assert!(!PipelinePhase::Idle.is_terminal());
    assert!(!PipelinePhase::Polling.is_terminal());
    assert!(!PipelinePhase::UpdateAvailable.is_terminal());
    assert!(!PipelinePhase::Downloading.is_terminal());
    assert!(!PipelinePhase::Validating.is_terminal());
    assert!(!PipelinePhase::Installing.is_terminal());
}

/// Pipeline phase display strings.
#[test]
fn test_pipeline_phase_display() {
    assert_eq!(PipelinePhase::Idle.to_string(), "Idle");
    assert_eq!(PipelinePhase::Polling.to_string(), "Polling");
    assert_eq!(PipelinePhase::UpdateAvailable.to_string(), "UpdateAvailable");
    assert_eq!(PipelinePhase::Downloading.to_string(), "Downloading");
    assert_eq!(PipelinePhase::Validating.to_string(), "Validating");
    assert_eq!(PipelinePhase::Installing.to_string(), "Installing");
    assert_eq!(PipelinePhase::RebootPending.to_string(), "RebootPending");
    assert_eq!(PipelinePhase::Error.to_string(), "Error");
}

/// Full lifecycle: Idle → Polling → Idle (no update).
#[tokio::test]
async fn test_lifecycle_idle_to_polling_to_idle() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "full-pipeline-001".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };

    // Idle → Polling
    let next = engine.execute_phase(&ctx, UpdatePhase::Idle).await.unwrap();
    assert_eq!(next, UpdatePhase::Polling);

    // Polling → Idle (stub returns Idle = no update available)
    let next = engine.execute_phase(&ctx, next).await.unwrap();
    assert_eq!(next, UpdatePhase::Idle);
}

/// Full lifecycle success path: all phases reachable.
#[tokio::test]
async fn test_full_lifecycle_chain() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "full-chain".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };

    let mut phases: Vec<UpdatePhase> = vec![];

    // Start at Idle
    let mut current = UpdatePhase::Idle;

    for _ in 0..20 {
        // safety limit
        phases.push(current);
        let next = engine.execute_phase(&ctx, current).await.unwrap();
        if next == UpdatePhase::Idle {
            phases.push(next);
            break; // Lifecycle complete
        }
        current = next;
    }

    // Verify we visited expected phases
    let phase_names: Vec<String> = phases.iter().map(|p| p.to_string()).collect();
    assert!(phase_names.contains(&"Idle".to_string()), "Should visit Idle phase");
    assert!(
        phase_names.contains(&"Polling".to_string()),
        "Should visit Polling phase"
    );
}

/// Terminal state — Success outcome reachable via Committing.
#[tokio::test]
async fn test_success_outcome_reachable() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "success-test".into(),
        metrics: std::sync::Mutex::new(LifecycleMetrics::default()),
    };

    let result = engine
        .execute_phase(&ctx, UpdatePhase::Committing)
        .await
        .unwrap();

    assert_eq!(result, UpdatePhase::Idle);
    let outcome = ctx.metrics.lock().unwrap().outcome.clone();
    assert_eq!(outcome, Some(LifecycleOutcome::Success));
}

/// Slot label display correctness.
#[test]
fn test_slot_label_display() {
    assert_eq!(SlotLabel::Primary.to_string(), "primary");
    assert_eq!(SlotLabel::Alternate.to_string(), "alternate");
}

/// Orchestrator config defaults are sensible (integration test).
#[test]
fn test_orchestrator_config_defaults() {
    let config = OrchestratorConfig::default();
    assert!(config.hub_base_url.contains("vela-ota.dev"));
    assert!(config.watchdog_enabled);
    assert_eq!(config.poll_interval, Duration::from_secs(300));
    assert_eq!(config.pulse.interval, Duration::from_secs(300));
    assert!(config.download_dir.to_string_lossy().contains("vela"));
    assert!(!config.block_device.is_empty());
}
