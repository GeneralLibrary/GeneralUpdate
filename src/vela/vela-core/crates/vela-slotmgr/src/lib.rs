#![forbid(unsafe_code)]
#![doc = "Dual-slot partition management for Vela OTA devices."]
#![doc = ""]
#![doc = "Provides A/B slot (Primary/Alternate) detection, boot flag persistence,"]
#![doc = "slot role swapping, and RAII fallback recovery."]

use thiserror::Error;

// Modules
pub mod guard;
pub mod linux;
pub mod manager;
pub mod mock;

// Re-exports
pub use guard::SlotRecoveryGuard;
pub use linux::{LinuxSlotConfig, LinuxSlotProvider};
pub use manager::{SlotLabel, SlotManager};
pub use mock::MockSlotProvider;

/// Errors during slot management operations.
#[derive(Error, Debug)]
pub enum SlotError {
    #[error("Slot layout detection failed: {0}")]
    DetectionFailed(String),

    #[error("Insufficient space on {device}: need {required}, have {available}")]
    InsufficientSpace {
        device: String,
        required: u64,
        available: u64,
    },

    #[error("Boot flag write failed: {0}")]
    BootFlagWriteError(String),

    #[error("Slot swap failed: {0}")]
    SwapFailed(String),

    #[error("IO error: {0}")]
    IoError(#[from] std::io::Error),
}

/// Result type alias for slot operations.
pub type SlotResult<T> = Result<T, SlotError>;

/// A/B slot identifiers.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, serde::Serialize, serde::Deserialize)]
pub enum SlotId {
    Primary,
    Alternate,
}

/// Filesystem types recognized by the slot manager.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileSystemType {
    Ext4,
    Btrfs,
    Xfs,
    F2fs,
    SquashFs,
    Unknown,
}

/// Information about a single slot.
#[derive(Debug, Clone)]
pub struct SlotInfo {
    pub id: SlotId,
    pub device_path: String,
    pub fs_type: FileSystemType,
    pub current_version: String,
    pub is_bootable: bool,
}

/// Information about a partition (for persistent data).
#[derive(Debug, Clone)]
pub struct PartitionInfo {
    pub device_path: String,
    pub fs_type: FileSystemType,
    pub total_bytes: u64,
    pub available_bytes: u64,
}

/// Slot layout describing the A/B configuration.
#[derive(Debug, Clone)]
pub struct SlotLayout {
    pub primary: SlotInfo,
    pub alternate: SlotInfo,
    pub persistent_data: Option<PartitionInfo>,
}

/// Boot flags controlling the bootloader behavior.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BootFlag {
    /// Attempt booting from the alternate slot.
    TryBoot,
    /// Confirm boot success; make the current slot permanent.
    CommitSuccess,
    /// Request fallback to the last successful slot.
    FallbackRequested,
}

/// Slot management provider trait.
#[async_trait::async_trait]
pub trait SlotProvider: Send + Sync {
    async fn detect_slots(&self) -> SlotResult<SlotLayout>;
    async fn get_active_slot(&self) -> SlotResult<SlotId>;
    async fn set_boot_flag(&self, flag: BootFlag) -> SlotResult<()>;
    async fn swap_slots(&self) -> SlotResult<()>;
}
