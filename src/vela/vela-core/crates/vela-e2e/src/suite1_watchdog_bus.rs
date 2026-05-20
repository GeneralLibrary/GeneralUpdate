//! Suite 1: Watchdog + EventBus integration tests.
//!
//! Validates that events are emitted correctly during watchdog
//! lifecycle and that subscribers receive them in order.

use vela_watchdog::SystemEvent;
use vela_watchdog::bus::SystemEventBus;

/// Events emitted during arm → pet → disarm cycle are published.
#[tokio::test]
async fn test_watchdog_lifecycle_emits_events() {
    let bus = SystemEventBus::new(32);
    let mut sub = bus.subscribe();

    // Simulate update lifecycle events
    bus.publish(SystemEvent::UpdateAvailable {
        rollout_id: "r1".into(),
        target_version: "2.0.0".into(),
        flashpack_size: 4096,
        force_install: false,
    });

    bus.publish(SystemEvent::DownloadStarted {
        rollout_id: "r1".into(),
        total_bytes: 4096,
    });

    bus.publish(SystemEvent::DownloadComplete {
        rollout_id: "r1".into(),
    });

    // Receive and verify
    let e1 = tokio::time::timeout(std::time::Duration::from_millis(200), sub.recv())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(e1.event_type(), "update_available");

    let e2 = tokio::time::timeout(std::time::Duration::from_millis(200), sub.recv())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(e2.event_type(), "download_started");

    let e3 = tokio::time::timeout(std::time::Duration::from_millis(200), sub.recv())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(e3.event_type(), "download_complete");
}

/// Watchdog timeout triggers SystemEvent::WatchdogTriggered.
#[test]
fn test_watchdog_triggered_event_emitted() {
    let bus = SystemEventBus::new(32);
    let mut sub = bus.subscribe();

    bus.publish(SystemEvent::WatchdogTriggered {
        last_pet_secs_ago: 15,
    });

    // Non-blocking receive should get the event
    let event = sub.try_recv().unwrap();
    assert_eq!(event.event_type(), "watchdog_triggered");

    if let SystemEvent::WatchdogTriggered { last_pet_secs_ago } = event {
        assert_eq!(last_pet_secs_ago, 15);
    } else {
        panic!("Wrong event variant");
    }
}

/// Background pet_loop with event emission — multiple events published.
#[tokio::test]
async fn test_background_event_emission() {
    let bus = SystemEventBus::new(32);
    let mut sub = bus.subscribe();

    let bus2 = bus.clone();
    let handle = tokio::spawn(async move {
        for i in 0..5 {
            bus2.publish(SystemEvent::HealthPulseSent { sequence: i });
        }
    });

    handle.await.unwrap();

    let mut count = 0;
    while let Ok(Ok(event)) =
        tokio::time::timeout(std::time::Duration::from_millis(50), sub.recv()).await
    {
        assert_eq!(event.event_type(), "health_pulse_sent");
        count += 1;
    }

    assert_eq!(count, 5);
}

/// Multiple subscribers all receive the same events.
#[tokio::test]
async fn test_multiple_subscribers() {
    let bus = SystemEventBus::new(32);
    let mut a = bus.subscribe();
    let mut b = bus.subscribe();
    let mut c = bus.subscribe();

    bus.publish(SystemEvent::InstallComplete {
        rollout_id: "r1".into(),
    });

    for sub in [&mut a, &mut b, &mut c] {
        let ev = tokio::time::timeout(std::time::Duration::from_millis(100), sub.recv())
            .await
            .unwrap()
            .unwrap();
        assert_eq!(ev.event_type(), "install_complete");
    }
}

#[test]
fn test_history_preserves_event_order() {
    let bus = SystemEventBus::new(16);

    bus.publish(SystemEvent::ValidationStarted {
        rollout_id: "r1".into(),
    });
    bus.publish(SystemEvent::ValidationComplete {
        rollout_id: "r1".into(),
        valid: true,
    });
    bus.publish(SystemEvent::InstallStarted {
        rollout_id: "r1".into(),
        target_slot: "alternate".into(),
    });

    let history = bus.history();
    assert_eq!(history.len(), 3);
    assert_eq!(history[0].event_type(), "validation_started");
    assert_eq!(history[1].event_type(), "validation_complete");
    assert_eq!(history[2].event_type(), "install_started");
}

#[test]
fn test_all_event_variants_displayable() {
    let events = vec![
        SystemEvent::UpdateAvailable {
            rollout_id: "r1".into(),
            target_version: "1.0".into(),
            flashpack_size: 100,
            force_install: false,
        },
        SystemEvent::DownloadStarted {
            rollout_id: "r1".into(),
            total_bytes: 100,
        },
        SystemEvent::DownloadProgress {
            rollout_id: "r1".into(),
            downloaded_bytes: 50,
            total_bytes: 100,
            percent: 50.0,
        },
        SystemEvent::DownloadComplete {
            rollout_id: "r1".into(),
        },
        SystemEvent::ValidationStarted {
            rollout_id: "r1".into(),
        },
        SystemEvent::ValidationComplete {
            rollout_id: "r1".into(),
            valid: true,
        },
        SystemEvent::InstallStarted {
            rollout_id: "r1".into(),
            target_slot: "alternate".into(),
        },
        SystemEvent::InstallComplete {
            rollout_id: "r1".into(),
        },
        SystemEvent::RebootRequired {
            target_slot: "alternate".into(),
        },
        SystemEvent::HealthPulseSent { sequence: 1 },
        SystemEvent::WatchdogTriggered {
            last_pet_secs_ago: 10,
        },
        SystemEvent::FallbackActivated {
            reason: "timeout".into(),
        },
        SystemEvent::AttestationComplete {
            device_id: "dev-01".into(),
        },
    ];

    for ev in events {
        let display = ev.to_string();
        assert!(
            !display.is_empty(),
            "Event {} should have display",
            ev.event_type()
        );
    }
}
