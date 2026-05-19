//! Suite 5: Error recovery scenarios.

use std::sync::Mutex;
use std::time::Duration;
use vela_hub::*;
use vela_hub::retry::RetryStrategy;
use vela_lifecycle::{
    LifecycleConfig, LifecycleContext, LifecycleEngine, LifecycleError, LifecycleMetrics,
    UpdatePhase,
};
use vela_slotmgr::{MockSlotProvider, SlotError, SlotLabel, SlotManager};

#[tokio::test]
async fn test_phase_timeout_configuration() {
    let mut config = LifecycleConfig::default();
    config.phase_timeouts.insert(UpdatePhase::Polling, Duration::from_nanos(1));
    let engine = LifecycleEngine::new(config);
    let ctx = LifecycleContext {
        update_id: "timeout-test".into(),
        metrics: Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine.execute_phase(&ctx, UpdatePhase::Polling).await;
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_fallback_returns_to_idle() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "fallback-test".into(),
        metrics: Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine.execute_phase(&ctx, UpdatePhase::FallbackRecovery).await.unwrap();
    assert_eq!(result, UpdatePhase::Idle);
}

#[tokio::test]
async fn test_error_preserves_idle_state() {
    let engine = LifecycleEngine::new(LifecycleConfig::default());
    let ctx = LifecycleContext {
        update_id: "error-test".into(),
        metrics: Mutex::new(LifecycleMetrics::default()),
    };
    let result = engine.execute_phase(&ctx, UpdatePhase::Polling).await.unwrap();
    assert_eq!(result, UpdatePhase::Idle);
    let metrics = ctx.metrics.lock().unwrap();
    assert!(metrics.outcome.is_none());
}

#[test]
fn test_insufficient_space_detected() {
    let mock = MockSlotProvider::new();
    mock.set_alternate_free_bytes(50);
    let mut mgr = SlotManager::with_mock(mock);
    let result = mgr.write_slot(SlotLabel::Alternate, &[0u8; 100]);
    assert!(result.is_err());
    if let Err(SlotError::InsufficientSpace { required, available, .. }) = result {
        assert_eq!(required, 100);
        assert_eq!(available, 50);
    } else {
        panic!("Expected InsufficientSpace error");
    }
}

#[test]
fn test_sufficient_space_succeeds() {
    let mock = MockSlotProvider::new();
    mock.set_alternate_free_bytes(1024);
    let mut mgr = SlotManager::with_mock(mock);
    let result = mgr.write_slot(SlotLabel::Alternate, &[0u8; 512]);
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_network_error_retry_exhaustion() {
    let strategy = RetryStrategy {
        max_retries: 1,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(5),
        jitter: 0.0,
    };
    let result: HubResult<()> = strategy
        .execute(|| async {
            Err(HubError::RateLimited(Duration::from_millis(1)))
        })
        .await;
    assert!(result.is_err());
}

#[tokio::test]
async fn test_rate_limit_triggers_retry() {
    let strategy = RetryStrategy {
        max_retries: 2,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(5),
        jitter: 0.0,
    };
    let result: HubResult<()> = strategy
        .execute(|| async { Err(HubError::RateLimited(Duration::from_secs(1))) })
        .await;
    assert!(result.is_err());
}

#[tokio::test]
async fn test_auth_error_fails_immediately() {
    let strategy = RetryStrategy {
        max_retries: 3,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(5),
        jitter: 0.0,
    };
    let result: HubResult<()> = strategy
        .execute(|| async { Err(HubError::AuthRequired) })
        .await;
    assert!(matches!(result, Err(HubError::AuthRequired)));
}

#[tokio::test]
async fn test_not_configured_fails_immediately() {
    let strategy = RetryStrategy::default();
    let result: HubResult<()> = strategy
        .execute(|| async { Err(HubError::NotConfigured) })
        .await;
    assert!(matches!(result, Err(HubError::NotConfigured)));
}

#[tokio::test]
async fn test_checksum_mismatch_fails_immediately() {
    let strategy = RetryStrategy::default();
    let result: HubResult<()> = strategy
        .execute(|| async {
            Err(HubError::ChecksumMismatch {
                expected: "abc".into(),
                actual: "xyz".into(),
            })
        })
        .await;
    assert!(matches!(result, Err(HubError::ChecksumMismatch { .. })));
}

#[test]
fn test_watchdog_timeout_fallback_path() {
    let bus = vela_watchdog::bus::SystemEventBus::new(32);
    bus.publish(vela_watchdog::SystemEvent::WatchdogTriggered { last_pet_secs_ago: 20 });
    bus.publish(vela_watchdog::SystemEvent::FallbackActivated {
        reason: "watchdog timeout during update".into(),
    });
    let history = bus.history();
    assert_eq!(history.len(), 2);
    assert_eq!(history[0].event_type(), "watchdog_triggered");
    assert_eq!(history[1].event_type(), "fallback_activated");
}

#[test]
fn test_lifecycle_retry_count_increments() {
    let ctx = LifecycleContext {
        update_id: "retry-test".into(),
        metrics: Mutex::new(LifecycleMetrics::default()),
    };
    assert_eq!(ctx.metrics.lock().unwrap().retry_count, 0);
    ctx.record_error(&LifecycleError::PhaseTimeout(UpdatePhase::Validating));
    assert_eq!(ctx.metrics.lock().unwrap().retry_count, 1);
    ctx.record_error(&LifecycleError::PhaseTimeout(UpdatePhase::Installing));
    assert_eq!(ctx.metrics.lock().unwrap().retry_count, 2);
}
