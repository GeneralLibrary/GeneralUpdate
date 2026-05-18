//! System event bus — in-process pub/sub for Vela subsystem coordination.
//!
//! Built on `tokio::sync::broadcast`. Multiple subscribers can receive
//! the same event. A circular history buffer retains the last N events
//! for late-joining subscribers and diagnostics.

use std::sync::{Arc, Mutex};
use tokio::sync::broadcast;
use tracing::{debug, info};

use crate::SystemEvent;

/// Capacity for the broadcast channel and history ring.
const DEFAULT_CAPACITY: usize = 128;

/// In-process event bus for publishing system events to subscribers.
///
/// Thread-safe, cloneable (shallow clones share the same bus).
#[derive(Clone)]
pub struct SystemEventBus {
    sender: broadcast::Sender<SystemEvent>,
    history: Arc<Mutex<circular_buffer::CircularBuffer<SystemEvent>>>,
}

impl Default for SystemEventBus {
    fn default() -> Self {
        Self::new(DEFAULT_CAPACITY)
    }
}

impl SystemEventBus {
    /// Create a new event bus with the given capacity.
    pub fn new(capacity: usize) -> Self {
        let (sender, _) = broadcast::channel(capacity);
        Self {
            sender,
            history: Arc::new(Mutex::new(
                circular_buffer::CircularBuffer::new(capacity),
            )),
        }
    }

    /// Publish an event to all subscribers.
    ///
    /// If no subscribers are active, the event is still recorded in history.
    /// Returns the number of subscribers that received the event.
    pub fn publish(&self, event: SystemEvent) -> usize {
        let name = event.event_type();
        let count = self.sender.receiver_count();

        if count > 0 {
            debug!(event = %name, subscribers = count, "Publishing event");
        }

        // Best-effort send — lagged receivers are dropped by broadcast
        let sent = self.sender.send(event.clone()).unwrap_or(0);

        // Record in history
        if let Ok(mut history) = self.history.lock() {
            history.push(event);
        }

        sent
    }

    /// Subscribe to system events.
    ///
    /// The subscriber receives events from the point of subscription
    /// onward. Use `subscribe_with_history` to also receive past events.
    pub fn subscribe(&self) -> Subscriber {
        Subscriber {
            rx: self.sender.subscribe(),
        }
    }

    /// Subscribe with replay of recent history.
    ///
    /// Returns the recent events AND a live subscriber handle.
    /// The history is drained before the live events start streaming.
    pub fn subscribe_with_history(&self) -> (Vec<SystemEvent>, Subscriber) {
        let history: Vec<SystemEvent> = self
            .history
            .lock()
            .map(|h| h.iter().cloned().collect())
            .unwrap_or_default();

        info!(history_len = history.len(), "Subscriber joined with history replay");
        (history, self.subscribe())
    }

    /// Number of active subscribers.
    pub fn subscriber_count(&self) -> usize {
        self.sender.receiver_count()
    }

    /// Dump the event history for diagnostics.
    pub fn history(&self) -> Vec<SystemEvent> {
        self.history
            .lock()
            .map(|h| h.iter().cloned().collect())
            .unwrap_or_default()
    }
}

/// A subscriber handle for receiving system events.
///
/// Use `recv().await` to wait for the next event.
/// Drop to unsubscribe.
#[derive(Debug)]
pub struct Subscriber {
    rx: broadcast::Receiver<SystemEvent>,
}

impl Subscriber {
    /// Receive the next event.
    ///
    /// Returns an error if the sender has been dropped (bus shut down).
    pub async fn recv(&mut self) -> Result<SystemEvent, broadcast::error::RecvError> {
        self.rx.recv().await
    }

    /// Try to receive without blocking.
    pub fn try_recv(&mut self) -> Result<SystemEvent, broadcast::error::TryRecvError> {
        self.rx.try_recv()
    }
}

// ─── simple circular buffer for history ─────────────────────────

mod circular_buffer {
    /// Fixed-size ring buffer that overwrites oldest entries when full.
    #[derive(Debug, Clone)]
    pub struct CircularBuffer<T> {
        buf: Vec<Option<T>>,
        write_pos: usize,
        count: usize,
    }

    impl<T: Clone> CircularBuffer<T> {
        pub fn new(capacity: usize) -> Self {
            let capacity = capacity.max(1);
            Self {
                buf: vec![None; capacity],
                write_pos: 0,
                count: 0,
            }
        }

        pub fn push(&mut self, item: T) {
            let cap = self.buf.len();
            self.buf[self.write_pos] = Some(item);
            self.write_pos = (self.write_pos + 1) % cap;
            if self.count < cap {
                self.count += 1;
            }
        }

        pub fn iter(&self) -> impl Iterator<Item = &T> {
            let cap = self.buf.len();
            let start = if self.count < cap {
                0
            } else {
                self.write_pos
            };
            (0..self.count).filter_map(move |i| {
                let idx = (start + i) % cap;
                self.buf[idx].as_ref()
            })
        }
    }

    #[cfg(test)]
    mod tests {
        use super::*;

        #[test]
        fn test_push_and_iter() {
            let mut cb = CircularBuffer::new(3);
            cb.push(1);
            cb.push(2);
            cb.push(3);
            assert_eq!(cb.iter().collect::<Vec<_>>(), vec![&1, &2, &3]);
        }

        #[test]
        fn test_overwrite() {
            let mut cb = CircularBuffer::new(3);
            cb.push(1);
            cb.push(2);
            cb.push(3);
            cb.push(4);
            assert_eq!(cb.iter().collect::<Vec<_>>(), vec![&2, &3, &4]);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bus_publish_and_history() {
        let bus = SystemEventBus::new(16);
        assert_eq!(bus.subscriber_count(), 0);

        bus.publish(SystemEvent::DownloadComplete {
            rollout_id: "roll-1".into(),
        });
        bus.publish(SystemEvent::InstallComplete {
            rollout_id: "roll-1".into(),
        });

        let history = bus.history();
        assert_eq!(history.len(), 2);
    }

    #[tokio::test]
    async fn test_subscriber_receives_events() {
        let bus = SystemEventBus::new(16);
        let mut sub = bus.subscribe();

        bus.publish(SystemEvent::DownloadComplete {
            rollout_id: "test".into(),
        });

        let event = tokio::time::timeout(
            std::time::Duration::from_millis(100),
            sub.recv(),
        )
        .await
        .unwrap()
        .unwrap();

        assert_eq!(event.event_type(), "download_complete");
    }

    #[tokio::test]
    async fn test_subscriber_with_history() {
        let bus = SystemEventBus::new(16);

        // Publish before subscribing
        bus.publish(SystemEvent::UpdateAvailable {
            rollout_id: "r1".into(),
            target_version: "1.0".into(),
            flashpack_size: 1024,
            force_install: false,
        });
        bus.publish(SystemEvent::DownloadComplete {
            rollout_id: "r1".into(),
        });

        // Subscribe with history
        let (history, mut sub) = bus.subscribe_with_history();
        assert_eq!(history.len(), 2);

        // Live event
        bus.publish(SystemEvent::InstallComplete {
            rollout_id: "r1".into(),
        });

        let event = tokio::time::timeout(
            std::time::Duration::from_millis(100),
            sub.recv(),
        )
        .await
        .unwrap()
        .unwrap();

        assert_eq!(event.event_type(), "install_complete");
    }

    #[test]
    fn test_event_display_formatting() {
        let ev = SystemEvent::UpdateAvailable {
            rollout_id: "r1".into(),
            target_version: "2.0".into(),
            flashpack_size: 4096,
            force_install: true,
        };
        let s = ev.to_string();
        assert!(s.contains("2.0"));
        assert!(s.contains("4096"));
        assert!(s.contains("force=true"));
    }
}
