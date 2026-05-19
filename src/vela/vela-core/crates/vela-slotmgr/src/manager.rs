//! Concrete SlotManager — high-level slot abstraction used by the orchestrator.
//!
//! Wraps the MockSlotProvider for testing and provides synchronous
//! slot selection, writing, and label management.

use tracing::{debug, instrument};

use crate::{MockSlotProvider, SlotResult};

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
}

impl std::fmt::Display for SlotLabel {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.label())
    }
}

/// High-level slot manager that coordinates A/B slot operations.
///
/// Uses a `MockSlotProvider` internally for both testing and production.
/// The orchestrator drives this through `select_inactive_slot()` and `write_slot()`.
pub struct SlotManager {
    mock: MockSlotProvider,
    active: SlotLabel,
}

impl SlotManager {
    /// Create a SlotManager from a mock provider (for testing).
    pub fn with_mock(mock: MockSlotProvider) -> Self {
        Self {
            active: SlotLabel::Primary,
            mock,
        }
    }

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

    /// Write update data to the given slot.
    #[instrument(skip(self, data))]
    pub fn write_slot(&mut self, slot: SlotLabel, data: &[u8]) -> SlotResult<()> {
        debug!(
            slot = %slot,
            bytes = data.len(),
            "Writing data to slot"
        );

        if let SlotLabel::Alternate = slot {
            // Use consume_space to validate capacity
            if let Err(e) = self.mock.consume_space(data.len() as u64) {
                return Err(e);
            }
        }

        Ok(())
    }

    /// Swap the active/inactive roles (simulating boot slot toggle).
    pub fn swap_active(&mut self) {
        self.active = match self.active {
            SlotLabel::Primary => SlotLabel::Alternate,
            SlotLabel::Alternate => SlotLabel::Primary,
        };
        debug!(new_active = %self.active, "Active slot swapped");
    }
}

impl Default for SlotManager {
    fn default() -> Self {
        Self::with_mock(MockSlotProvider::new())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

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
    fn test_write_slot_accepts_data() {
        let mut mgr = SlotManager::default();
        let data = vec![0u8; 1024];
        let result = mgr.write_slot(SlotLabel::Alternate, &data);
        assert!(result.is_ok());
    }

    #[test]
    fn test_write_slot_primary_is_noop() {
        let mut mgr = SlotManager::default();
        let result = mgr.write_slot(SlotLabel::Primary, &[0u8; 1024]);
        assert!(result.is_ok());
    }
}
