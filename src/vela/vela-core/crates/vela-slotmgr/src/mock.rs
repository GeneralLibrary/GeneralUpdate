//! Mock slot provider for unit testing.
//!
//! Provides a fully in-memory slot provider that can be used to
//! test slot management logic without real hardware.

use std::sync::{Arc, Mutex};

use tracing::{debug, info, instrument};

use crate::{
    BootFlag, FileSystemType, PartitionInfo, SlotError, SlotId, SlotInfo, SlotLayout,
    SlotProvider, SlotResult,
};

/// Internal state of the mock provider.
#[derive(Debug, Clone)]
struct MockState {
    primary_version: String,
    alternate_version: String,
    active_slot: SlotId,
    boot_flag: Option<BootFlag>,
    alternate_free_bytes: u64,
    alternate_total_bytes: u64,
}

/// In-memory slot provider for testing.
///
/// All state is held in `Arc<Mutex<>>` so it can be shared and inspected.
pub struct MockSlotProvider {
    state: Arc<Mutex<MockState>>,
}

impl MockSlotProvider {
    /// Create a new mock provider with default state.
    pub fn new() -> Self {
        Self {
            state: Arc::new(Mutex::new(MockState {
                primary_version: "1.0.0".into(),
                alternate_version: "1.0.0".into(),
                active_slot: SlotId::Primary,
                boot_flag: None,
                alternate_free_bytes: 1024 * 1024 * 1024, // 1 GiB
                alternate_total_bytes: 2 * 1024 * 1024 * 1024, // 2 GiB
            })),
        }
    }

    /// Create a provider with specific versions.
    pub fn with_versions(primary: &str, alternate: &str) -> Self {
        let provider = Self::new();
        {
            let mut state = provider.state.lock().unwrap();
            state.primary_version = primary.to_string();
            state.alternate_version = alternate.to_string();
        }
        provider
    }

    /// Get a clone of the internal state for assertion.
    pub fn snapshot(&self) -> MockState {
        self.state.lock().unwrap().clone()
    }

    /// Set the free space on the alternate slot.
    pub fn set_alternate_free_bytes(&self, bytes: u64) {
        self.state.lock().unwrap().alternate_free_bytes = bytes;
    }

    /// Simulate consuming space on the alternate slot.
    pub fn consume_space(&self, bytes: u64) -> SlotResult<()> {
        let mut state = self.state.lock().unwrap();
        if bytes > state.alternate_free_bytes {
            return Err(SlotError::InsufficientSpace {
                device: "/dev/mock-p3".into(),
                required: bytes,
                available: state.alternate_free_bytes,
            });
        }
        state.alternate_free_bytes -= bytes;
        Ok(())
    }
}

impl Default for MockSlotProvider {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait::async_trait]
impl SlotProvider for MockSlotProvider {
    #[instrument(skip(self))]
    async fn detect_slots(&self) -> SlotResult<SlotLayout> {
        let state = self.state.lock().unwrap();
        debug!("Detecting mock slot layout");

        Ok(SlotLayout {
            primary: SlotInfo {
                id: SlotId::Primary,
                device_path: "/dev/mock-p2".into(),
                fs_type: FileSystemType::Ext4,
                current_version: state.primary_version.clone(),
                is_bootable: true,
            },
            alternate: SlotInfo {
                id: SlotId::Alternate,
                device_path: "/dev/mock-p3".into(),
                fs_type: FileSystemType::Ext4,
                current_version: state.alternate_version.clone(),
                is_bootable: true,
            },
            persistent_data: Some(PartitionInfo {
                device_path: "/dev/mock-p4".into(),
                fs_type: FileSystemType::Ext4,
                total_bytes: state.alternate_total_bytes,
                available_bytes: state.alternate_free_bytes,
            }),
        })
    }

    #[instrument(skip(self))]
    async fn get_active_slot(&self) -> SlotResult<SlotId> {
        let state = self.state.lock().unwrap();
        debug!(active = ?state.active_slot, "Getting active slot");
        Ok(state.active_slot)
    }

    #[instrument(skip(self))]
    async fn set_boot_flag(&self, flag: BootFlag) -> SlotResult<()> {
        let mut state = self.state.lock().unwrap();
        info!(?flag, "Setting boot flag");
        state.boot_flag = Some(flag);
        Ok(())
    }

    #[instrument(skip(self))]
    async fn swap_slots(&self) -> SlotResult<()> {
        let mut state = self.state.lock().unwrap();
        info!("Swapping slots");

        // Swap versions using temp to avoid double mutable borrow
        let tmp = state.primary_version.clone();
        state.primary_version = state.alternate_version.clone();
        state.alternate_version = tmp;

        // Toggle active slot
        state.active_slot = match state.active_slot {
            SlotId::Primary => SlotId::Alternate,
            SlotId::Alternate => SlotId::Primary,
        };

        state.boot_flag = Some(BootFlag::CommitSuccess);
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_mock_default_primary() {
        let provider = MockSlotProvider::new();
        assert_eq!(provider.get_active_slot().await.unwrap(), SlotId::Primary);
    }

    #[tokio::test]
    async fn test_mock_detect_layout() {
        let provider = MockSlotProvider::with_versions("1.0.0", "2.0.0");
        let layout = provider.detect_slots().await.unwrap();
        assert_eq!(layout.primary.current_version, "1.0.0");
        assert_eq!(layout.alternate.current_version, "2.0.0");
        assert_eq!(layout.alternate.device_path, "/dev/mock-p3");
    }

    #[tokio::test]
    async fn test_mock_set_boot_flag() {
        let provider = MockSlotProvider::new();
        provider.set_boot_flag(BootFlag::TryBoot).await.unwrap();
        assert_eq!(provider.snapshot().boot_flag, Some(BootFlag::TryBoot));
    }

    #[tokio::test]
    async fn test_mock_swap_slots() {
        let provider = MockSlotProvider::with_versions("1.0.0", "2.0.0");
        provider.swap_slots().await.unwrap();

        let state = provider.snapshot();
        assert_eq!(state.primary_version, "2.0.0");
        assert_eq!(state.alternate_version, "1.0.0");
        assert_eq!(state.active_slot, SlotId::Alternate);
    }

    #[tokio::test]
    async fn test_mock_consume_space() {
        let provider = MockSlotProvider::new();
        provider.set_alternate_free_bytes(1000);

        // Should succeed
        assert!(provider.consume_space(500).is_ok());

        // Should fail — not enough space
        assert!(provider.consume_space(600).is_err());
    }
}
