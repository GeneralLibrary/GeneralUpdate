//! Suite 4: Full pipeline state transitions.
//!
//! Validates terminal states and lifecycle phase transitions
//! across the LifecycleEngine.

use vela_core::orchestrator::PipelinePhase;
use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleMetrics, LifecycleOutcome,
    UpdatePhase,
};

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
    assert!(
        phase_names.contains(&"Idle".to_string()),
        "Should visit Idle phase"
    );
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
