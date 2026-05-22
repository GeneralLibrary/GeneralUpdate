//! Concrete SlotManager — high-level slot abstraction used by the orchestrator.
//!
//! Generic over `SlotProvider`, defaulting to `MockSlotProvider` for testing.
//! Provides synchronous slot selection, real block-device writing via
//! `vela_flasher::BlockDeviceWriter`, and label management.

use tracing::{debug, info, instrument, warn};

use crate::{MockSlotProvider, SlotError, SlotId, SlotLayout, SlotProvider, SlotResult};
use vela_flasher::{BlockDeviceWriter, FlashConfig};

/// Label for a specific slot partition.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SlotLabel {
    Primary,
    Alternate,
}

impl SlotLabel {
    /// Human-readable slot label.
    pub fn label(&self) -> &str {
        match self {
            Self::Primary => "primary",
            Self::Alternate => "alternate",
        }
    }

    /// Convert to SlotId.
    pub fn to_slot_id(&self) -> SlotId {
        match self {
            Self::Primary => SlotId::Primary,
            Self::Alternate => SlotId::Alternate,
        }
    }
}

impl std::fmt::Display for SlotLabel {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.label())
    }
}

/// High-level slot manager that coordinates A/B slot operations.
///
/// Generic over the `SlotProvider` trait so callers can plug in
/// real Linux block-device detection or a mock for testing.
/// Defaults to `MockSlotProvider`.
pub struct SlotManager<P: SlotProvider + ?Sized = MockSlotProvider> {
    provider: Box<P>,
    active: SlotLabel,
    /// Cached slot layout, refreshed via `refresh()`.
    layout: Option<SlotLayout>,
    /// Override for the target device path (for testing).
    device_override: Option<String>,
}

impl SlotManager<MockSlotProvider> {
    /// Create a SlotManager backed by a `MockSlotProvider` (for testing).
    pub fn with_mock(mock: MockSlotProvider) -> Self {
        Self {
            provider: Box::new(mock),
            active: SlotLabel::Primary,
            layout: None,
            device_override: None,
        }
    }
}

impl Default for SlotManager<MockSlotProvider> {
    fn default() -> Self {
        Self::with_mock(MockSlotProvider::new())
    }
}

impl<P: SlotProvider + ?Sized> SlotManager<P> {
    /// Select the non-active (inactive) slot — the target for installation.
    #[instrument(skip(self))]
    pub fn select_inactive_slot(&self) -> SlotLabel {
        match self.active {
            SlotLabel::Primary => SlotLabel::Alternate,
            SlotLabel::Alternate => SlotLabel::Primary,
        }
    }

    /// Get the currently active slot.
    pub fn active_slot(&self) -> SlotLabel {
        self.active
    }

    /// Refresh the cached slot layout by calling `detect_slots()` on the provider.
    ///
    /// Call this before `write_slot` to ensure the layout is up-to-date.
    pub fn refresh(&mut self) -> SlotResult<()> {
        // This is a synchronous wrapper; in production, callers should use
        // an async-aware wrapper. We create a single-threaded tokio runtime
        // to bridge the async provider trait to sync callers.
        let rt = tokio::runtime::Builder::new_current_thread()
            .enable_time()
            .build()
            .map_err(|e| SlotError::DetectionFailed(format!("failed to create runtime: {e}")))?;
        let layout = rt.block_on(self.provider.detect_slots())?;
        debug!(
            primary = %layout.primary.device_path,
            alternate = %layout.alternate.device_path,
            "Slot layout refreshed"
        );
        self.layout = Some(layout);
        Ok(())
    }

    /// Get the device path for a given slot label.
    ///
    /// Returns the device path from the cached layout, or the override if set.
    pub fn device_path(&self, slot: SlotLabel) -> Option<String> {
        if let Some(ref ov) = self.device_override {
            return Some(ov.clone());
        }
        self.layout.as_ref().map(|layout| match slot {
            SlotLabel::Primary => layout.primary.device_path.clone(),
            SlotLabel::Alternate => layout.alternate.device_path.clone(),
        })
    }

    /// Get the current version installed in a given slot.
    ///
    /// Returns `None` if the layout hasn't been refreshed yet.
    pub fn slot_version(&self, slot: SlotLabel) -> Option<String> {
        self.layout.as_ref().map(|layout| match slot {
            SlotLabel::Primary => layout.primary.current_version.clone(),
            SlotLabel::Alternate => layout.alternate.current_version.clone(),
        })
    }

    /// Swap the active/inactive roles (simulating boot slot toggle),
    /// and call `swap_slots()` on the provider to persist the change.
    ///
    /// Returns an error if the provider's `swap_slots()` fails.
    pub fn swap_and_commit(&mut self) -> SlotResult<()> {
        // Persist the swap via the provider
        let rt = tokio::runtime::Builder::new_current_thread()
            .enable_time()
            .build()
            .map_err(|e| SlotError::SwapFailed(format!("failed to create runtime: {e}")))?;
        rt.block_on(self.provider.swap_slots())?;

        // Update local state
        self.active = match self.active {
            SlotLabel::Primary => SlotLabel::Alternate,
            SlotLabel::Alternate => SlotLabel::Primary,
        };
        debug!(new_active = %self.active, "Active slot swapped and committed");
        Ok(())
    }

    /// Override the detected slot layout with a custom one.
    ///
    /// Useful for testing scenarios where the real layout is unavailable.
    pub fn override_layout(&mut self, layout: SlotLayout) {
        debug!(
            primary = %layout.primary.device_path,
            alternate = %layout.alternate.device_path,
            "Slot layout manually overridden"
        );
        self.layout = Some(layout);
    }

    /// Override the device path used for writing (for testing with temp files).
    pub fn set_device_override(&mut self, path: Option<String>) {
        self.device_override = path;
    }

    /// Write firmware data to the given slot using `BlockDeviceWriter`.
    ///
    /// The device path is derived from the cached slot layout, or from
    /// an explicitly set device override. If a `FlashConfig` is provided,
    /// it will be used; otherwise a default config is constructed from the
    /// detected or overridden device path.
    #[instrument(skip(self, data))]
    pub fn write_slot(
        &mut self,
        slot: SlotLabel,
        data: &[u8],
        config: Option<FlashConfig>,
    ) -> SlotResult<u64> {
        let device = self
            .device_path(slot)
            .ok_or_else(|| SlotError::DetectionFailed("no device path for slot".into()))?;

        debug!(
            slot = %slot,
            device = %device,
            bytes = data.len(),
            "Writing data to slot"
        );

        let flash_config = config.unwrap_or_else(|| FlashConfig::new(&device));
        let mut writer = BlockDeviceWriter::new(flash_config);

        let bytes_written = writer
            .write_image(data, None)
            .map_err(|e| SlotError::FlashError(Box::new(e)))?;

        info!(bytes = bytes_written, "Slot write completed");
        Ok(bytes_written)
    }

    /// Swap the active slot in memory (without calling the provider).
    ///
    /// This is a lightweight swap for testing; use `swap_and_commit()`
    /// for a real persisted swap.
    pub fn swap_active(&mut self) {
        self.active = match self.active {
            SlotLabel::Primary => SlotLabel::Alternate,
            SlotLabel::Alternate => SlotLabel::Primary,
        };
        debug!(new_active = %self.active, "Active slot swapped (in-memory)");
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::SlotLayout;
    use tempfile::NamedTempFile;

    #[test]
    fn test_slot_label_display() {
        assert_eq!(SlotLabel::Primary.label(), "primary");
        assert_eq!(SlotLabel::Alternate.label(), "alternate");
        assert_eq!(SlotLabel::Primary.to_string(), "primary");
    }

    #[test]
    fn test_select_inactive() {
        let mgr = SlotManager::default();
        assert_eq!(mgr.select_inactive_slot(), SlotLabel::Alternate);
    }

    #[test]
    fn test_swap_active() {
        let mut mgr = SlotManager::default();
        assert_eq!(mgr.active_slot(), SlotLabel::Primary);

        mgr.swap_active();
        assert_eq!(mgr.active_slot(), SlotLabel::Alternate);
        assert_eq!(mgr.select_inactive_slot(), SlotLabel::Primary);

        mgr.swap_active();
        assert_eq!(mgr.active_slot(), SlotLabel::Primary);
    }

    #[test]
    fn test_write_slot_uses_temp_device() {
        let tmp = NamedTempFile::new().unwrap();
        let dev_path = tmp.path().to_string_lossy().to_string();

        let mut mgr = SlotManager::default();
        mgr.set_device_override(Some(dev_path));

        let data = vec![0xDEu8; 1024];
        let bytes = mgr.write_slot(SlotLabel::Alternate, &data, None).unwrap();
        assert_eq!(bytes, 1024);
    }

    #[test]
    fn test_write_slot_with_custom_config() {
        let tmp = NamedTempFile::new().unwrap();
        let dev_path = tmp.path().to_string_lossy().to_string();

        let mut mgr = SlotManager::default();
        mgr.set_device_override(Some(dev_path.clone()));

        let config = FlashConfig::new(&dev_path)
            .chunk_size(128)
            .sync_after_chunk(false)
            .verify_after_write(true);

        let data = vec![0xADu8; 512];
        let bytes = mgr
            .write_slot(SlotLabel::Alternate, &data, Some(config))
            .unwrap();
        assert_eq!(bytes, 512);

        // Verify read-back: the file should contain the written data
        let written = std::fs::read(tmp.path()).unwrap();
        assert_eq!(&written[..512], data.as_slice());
    }

    #[test]
    fn test_write_slot_no_device_path() {
        let mut mgr = SlotManager::default();
        // No layout and no device override -> should fail
        let data = vec![0u8; 100];
        let result = mgr.write_slot(SlotLabel::Alternate, &data, None);
        assert!(result.is_err());
    }

    #[test]
    fn test_refresh_populates_layout() {
        let mut mgr = SlotManager::default();
        assert!(mgr.layout.is_none());

        mgr.refresh().unwrap();
        assert!(mgr.layout.is_some());

        let layout = mgr.layout.as_ref().unwrap();
        assert!(layout.primary.device_path.contains("mock-p2"));
        assert!(layout.alternate.device_path.contains("mock-p3"));
    }

    #[test]
    fn test_device_path_after_refresh() {
        let mut mgr = SlotManager::default();
        mgr.refresh().unwrap();

        assert_eq!(
            mgr.device_path(SlotLabel::Primary).unwrap(),
            "/dev/mock-p2"
        );
        assert_eq!(
            mgr.device_path(SlotLabel::Alternate).unwrap(),
            "/dev/mock-p3"
        );
    }

    #[test]
    fn test_slot_version_after_refresh() {
        let mock = MockSlotProvider::with_versions("1.5.0", "2.0.0");
        let mut mgr = SlotManager::with_mock(mock);
        mgr.refresh().unwrap();

        assert_eq!(mgr.slot_version(SlotLabel::Primary).unwrap(), "1.5.0");
        assert_eq!(mgr.slot_version(SlotLabel::Alternate).unwrap(), "2.0.0");
    }

    #[test]
    fn test_device_override_takes_precedence() {
        let mut mgr = SlotManager::default();
        mgr.refresh().unwrap();

        // Layout says /dev/mock-p2 and /dev/mock-p3
        assert_eq!(
            mgr.device_path(SlotLabel::Primary).unwrap(),
            "/dev/mock-p2"
        );

        // Override should take precedence
        mgr.set_device_override(Some("/tmp/test_override".into()));
        assert_eq!(
            mgr.device_path(SlotLabel::Primary).unwrap(),
            "/tmp/test_override"
        );
        assert_eq!(
            mgr.device_path(SlotLabel::Alternate).unwrap(),
            "/tmp/test_override"
        );
    }

    #[test]
    fn test_override_layout() {
        let mut mgr = SlotManager::default();
        let custom = SlotLayout {
            primary: crate::SlotInfo {
                id: crate::SlotId::Primary,
                device_path: "/dev/custom-p1".into(),
                fs_type: crate::FileSystemType::Ext4,
                current_version: "3.0.0".into(),
                is_bootable: true,
            },
            alternate: crate::SlotInfo {
                id: crate::SlotId::Alternate,
                device_path: "/dev/custom-p2".into(),
                fs_type: crate::FileSystemType::Ext4,
                current_version: "3.1.0".into(),
                is_bootable: true,
            },
            persistent_data: None,
        };

        mgr.override_layout(custom);
        assert_eq!(
            mgr.device_path(SlotLabel::Primary).unwrap(),
            "/dev/custom-p1"
        );
        assert_eq!(
            mgr.slot_version(SlotLabel::Alternate).unwrap(),
            "3.1.0"
        );
    }

    #[test]
    fn test_write_slot_primary_is_noop_capacity_check() {
        // Writing to the primary slot is allowed — it's up to the caller
        // to decide whether that's safe.
        let tmp = NamedTempFile::new().unwrap();
        let mut mgr = SlotManager::default();
        mgr.set_device_override(Some(tmp.path().to_string_lossy().to_string()));

        let result = mgr.write_slot(SlotLabel::Primary, &[0u8; 1024], None);
        assert!(result.is_ok());
    }

    #[test]
    fn test_slot_label_to_slot_id() {
        assert_eq!(SlotLabel::Primary.to_slot_id(), SlotId::Primary);
        assert_eq!(SlotLabel::Alternate.to_slot_id(), SlotId::Alternate);
    }
}
