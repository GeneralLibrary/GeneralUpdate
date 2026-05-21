//! Suite 5: Error recovery scenarios.
//!
//! Cross-crate error recovery: lifecycle timeout handling and
//! watchdog-triggered fallback event chain.

use std::sync::Mutex;
use std::time::Duration;
use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleMetrics, UpdatePhase,
};

#[tokio::test]
async fn test_phase_timeout_configuration() {
    let mut config = LifecycleConfig::default();
    config
        .phase_timeouts
        .insert(UpdatePhase::Polling, Duration::from_nanos(1));
    let engine = LifecycleEngine::new(config);
    let ctx = LifecycleContext {
        update_id: "timeout-test".into(),
        metrics: Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine.execute_phase(&ctx, UpdatePhase::Polling).await;
    assert!(result.is_ok());
}

#[test]
fn test_watchdog_timeout_fallback_path() {
    let bus = vela_watchdog::bus::SystemEventBus::new(32);
    bus.publish(vela_watchdog::SystemEvent::WatchdogTriggered {
        last_pet_secs_ago: 20,
    });
    bus.publish(vela_watchdog::SystemEvent::FallbackActivated {
        reason: "watchdog timeout during update".into(),
    });
    let history = bus.history();
    assert_eq!(history.len(), 2);
    assert_eq!(history[0].event_type(), "watchdog_triggered");
    assert_eq!(history[1].event_type(), "fallback_activated");
}
