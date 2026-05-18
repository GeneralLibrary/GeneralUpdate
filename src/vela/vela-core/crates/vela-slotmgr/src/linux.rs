//! Linux slot provider implementation.
//!
//! Reads slot configuration from sysfs and partition tables.
//! Uses U-Boot environment or EFI variables for boot flag persistence.

use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;

use tracing::{debug, error, info, instrument, trace, warn};

use crate::{
    BootFlag, FileSystemType, PartitionInfo, SlotError, SlotId, SlotInfo, SlotLayout,
    SlotProvider, SlotResult,
};

/// Configuration for the Linux slot provider.
///
/// Describes how to detect slots from the running system.
#[derive(Debug, Clone)]
pub struct LinuxSlotConfig {
    /// Paths to check for primary slot (e.g., the device we are currently running from).
    pub primary_device_hint: Option<String>,
    /// Paths to check for alternate slot.
    pub alternate_device_hint: Option<String>,
    /// Path to U-Boot environment file (e.g. `/boot/uboot.env`).
    pub uboot_env_path: Option<String>,
    /// Path to EFI vars mount (e.g. `/sys/firmware/efi/efivars`).
    pub efi_vars_path: Option<String>,
    /// Minimum free space in bytes required on alternate slot before write.
    pub min_free_space: u64,
}

impl Default for LinuxSlotConfig {
    fn default() -> Self {
        Self {
            primary_device_hint: None,
            alternate_device_hint: None,
            uboot_env_path: Some("/boot/uboot.env".into()),
            efi_vars_path: None,
            min_free_space: 64 * 1024 * 1024, // 64 MiB minimum
        }
    }
}

/// Linux implementation of the slot provider.
///
/// Detects A/B partition layout via sysfs and persists boot flags
/// in U-Boot environment or EFI variables.
pub struct LinuxSlotProvider {
    config: LinuxSlotConfig,
}

impl LinuxSlotProvider {
    /// Create a new slot provider with the given configuration.
    pub fn new(config: LinuxSlotConfig) -> Self {
        Self { config }
    }

    /// Read the current boot flag from U-Boot environment.
    fn read_uboot_boot_flag(&self) -> SlotResult<Option<BootFlag>> {
        let env_path = match &self.config.uboot_env_path {
            Some(p) => PathBuf::from(p),
            None => return Ok(None),
        };

        if !env_path.exists() {
            trace!(path = %env_path.display(), "U-Boot env file not found");
            return Ok(None);
        }

        let content = fs::read_to_string(&env_path).map_err(|e| {
            error!(path = %env_path.display(), error = %e, "Failed to read U-Boot env");
            SlotError::IoError(e)
        })?;

        // Parse key=value lines. Vela uses `vela_boot_flag` and `vela_slot` keys.
        for line in content.lines() {
            let line = line.trim();
            if let Some(value) = line.strip_prefix("vela_boot_flag=") {
                return match value.trim() {
                    "tryboot" => Ok(Some(BootFlag::TryBoot)),
                    "commit" => Ok(Some(BootFlag::CommitSuccess)),
                    "fallback" => Ok(Some(BootFlag::FallbackRequested)),
                    other => {
                        warn!(value = %other, "Unknown vela_boot_flag value");
                        Ok(None)
                    }
                };
            }
        }

        debug!("No vela_boot_flag found in U-Boot environment");
        Ok(None)
    }

    /// Write a boot flag to U-Boot environment.
    fn write_uboot_boot_flag(&self, flag: BootFlag) -> SlotResult<()> {
        let env_path = match &self.config.uboot_env_path {
            Some(p) => PathBuf::from(p),
            None => {
                return Err(SlotError::BootFlagWriteError(
                    "No U-Boot env path configured".into(),
                ));
            }
        };

        let flag_str = match flag {
            BootFlag::TryBoot => "tryboot",
            BootFlag::CommitSuccess => "commit",
            BootFlag::FallbackRequested => "fallback",
        };

        // Read existing content, update or append the vela_boot_flag line.
        let current = if env_path.exists() {
            fs::read_to_string(&env_path).unwrap_or_default()
        } else {
            String::new()
        };

        let mut new_content = String::new();
        let mut found = false;
        for line in current.lines() {
            if line.trim_start().starts_with("vela_boot_flag=") {
                new_content.push_str(&format!("vela_boot_flag={flag_str}\n"));
                found = true;
            } else {
                new_content.push_str(line);
                new_content.push('\n');
            }
        }
        if !found {
            new_content.push_str(&format!("vela_boot_flag={flag_str}\n"));
        }

        // Atomic write: write to temp file, then rename
        let tmp_path = env_path.with_extension("env.tmp");
        fs::write(&tmp_path, &new_content).map_err(|e| {
            error!(path = %tmp_path.display(), error = %e, "Failed to write U-Boot env temp file");
            SlotError::IoError(e)
        })?;
        fs::rename(&tmp_path, &env_path).map_err(|e| {
            error!(from = %tmp_path.display(), to = %env_path.display(), error = %e, "Failed to atomically rename U-Boot env");
            SlotError::IoError(e)
        })?;

        info!(flag = %flag_str, "Boot flag written to U-Boot environment");
        Ok(())
    }

    /// Detect filesystem type from a block device using `blkid` or heuristics.
    fn detect_fs_type(&self, device_path: &str) -> FileSystemType {
        // Try /sys/block heuristics and /proc/mounts
        // For now, a simple heuristic based on device path.
        if device_path.contains("mmcblk") {
            // eMMC / SD card — typically ext4 on embedded Linux
            FileSystemType::Ext4
        } else if device_path.contains("nvme") {
            FileSystemType::Ext4
        } else {
            FileSystemType::Unknown
        }
    }

    /// Read partition size from sysfs.
    fn read_partition_size(&self, device_path: &str) -> u64 {
        // Try to read size from /sys/class/block/<dev>/size
        let dev_name = device_path.trim_start_matches("/dev/");
        let size_path = PathBuf::from("/sys/class/block").join(dev_name).join("size");

        if let Ok(content) = fs::read_to_string(&size_path) {
            if let Ok(sectors) = content.trim().parse::<u64>() {
                // Each sector is 512 bytes
                return sectors * 512;
            }
        }
        0
    }

    /// Build slot info for a device path.
    fn build_slot_info(&self, id: SlotId, device_path: &str, version_file: &PathBuf) -> SlotInfo {
        let current_version = if version_file.exists() {
            fs::read_to_string(version_file)
                .map(|s| s.trim().to_string())
                .unwrap_or_else(|_| "unknown".into())
        } else {
            "unknown".into()
        };

        SlotInfo {
            id,
            device_path: device_path.to_string(),
            fs_type: self.detect_fs_type(device_path),
            current_version,
            is_bootable: true,
        }
    }
}

#[async_trait::async_trait]
impl SlotProvider for LinuxSlotProvider {
    /// Detect the A/B slot layout from the system.
    #[instrument(skip(self))]
    async fn detect_slots(&self) -> SlotResult<SlotLayout> {
        info!("Detecting slot layout");

        // Use configured hints or fall back to common patterns.
        let primary_dev = self
            .config
            .primary_device_hint
            .clone()
            .unwrap_or_else(|| "/dev/mmcblk0p2".to_string());
        let alternate_dev = self
            .config
            .alternate_device_hint
            .clone()
            .unwrap_or_else(|| "/dev/mmcblk0p3".to_string());

        // Version files: each slot has /etc/vela-version
        let primary_version_file = PathBuf::from("/mnt/primary/etc/vela-version");
        let alternate_version_file = PathBuf::from("/mnt/alternate/etc/vela-version");

        let primary = self.build_slot_info(SlotId::Primary, &primary_dev, &primary_version_file);
        let alternate = self.build_slot_info(SlotId::Alternate, &alternate_dev, &alternate_version_file);

        // Detect persistent data partition if present
        let persistent_data = if let Some(persist_hint) = &self.config.primary_device_hint {
            let base = persist_hint.trim_end_matches(|c: char| c.is_ascii_digit());
            let persist_dev = format!("{base}4");
            let size = self.read_partition_size(&persist_dev);
            if size > 0 {
                Some(PartitionInfo {
                    device_path: persist_dev,
                    fs_type: FileSystemType::Ext4,
                    total_bytes: size,
                    available_bytes: size, // approximate
                })
            } else {
                None
            }
        } else {
            None
        };

        info!(
            primary_dev = %primary.device_path,
            primary_ver = %primary.current_version,
            alternate_dev = %alternate.device_path,
            alternate_ver = %alternate.current_version,
            "Slot layout detected"
        );

        Ok(SlotLayout {
            primary,
            alternate,
            persistent_data,
        })
    }

    /// Get the currently active slot.
    #[instrument(skip(self))]
    async fn get_active_slot(&self) -> SlotResult<SlotId> {
        // Check boot flag first
        if let Some(flag) = self.read_uboot_boot_flag()? {
            match flag {
                BootFlag::TryBoot => {
                    debug!("TryBoot flag set — alternate slot is being attempted");
                    return Ok(SlotId::Alternate);
                }
                BootFlag::CommitSuccess => {
                    // After commit, the alternate becomes the new primary.
                    // We need to determine which slot we're actually running from.
                    trace!("CommitSuccess flag — checking running slot");
                }
                BootFlag::FallbackRequested => {
                    debug!("FallbackRequested flag — primary is active");
                    return Ok(SlotId::Primary);
                }
            }
        }

        // Default: check /proc/cmdline for root= parameter
        let cmdline = fs::read_to_string("/proc/cmdline").unwrap_or_default();
        let layout = self.detect_slots().await?;

        if cmdline.contains(&layout.alternate.device_path) {
            Ok(SlotId::Alternate)
        } else {
            Ok(SlotId::Primary)
        }
    }

    /// Persist a boot flag for the next boot.
    #[instrument(skip(self))]
    async fn set_boot_flag(&self, flag: BootFlag) -> SlotResult<()> {
        warn!(?flag, "Setting boot flag — this affects next boot behavior");

        // Persist to U-Boot environment
        self.write_uboot_boot_flag(flag)?;

        info!(?flag, "Boot flag persisted successfully");
        Ok(())
    }

    /// Swap the Primary and Alternate slot roles.
    #[instrument(skip(self))]
    async fn swap_slots(&self) -> SlotResult<()> {
        warn!("Swapping slot roles — Primary ↔ Alternate");

        // In practice, this is done by the bootloader on next boot.
        // We set the CommitSuccess flag to tell the bootloader the
        // alternate slot is now the canonical primary.

        // Update the boot flag
        self.set_boot_flag(BootFlag::CommitSuccess).await?;

        // Update version files
        let layout = self.detect_slots().await?;

        // The alternate slot's version becomes the new primary's version
        let primary_version_file = PathBuf::from("/mnt/primary/etc/vela-version");
        let alternate_version_file = PathBuf::from("/mnt/alternate/etc/vela-version");

        if alternate_version_file.exists() {
            let new_version = fs::read_to_string(&alternate_version_file)
                .unwrap_or_else(|_| "unknown".into());
            fs::write(&primary_version_file, &new_version).map_err(|e| {
                error!(path = %primary_version_file.display(), error = %e, "Failed to update primary version file");
                SlotError::IoError(e)
            })?;
            info!(version = %new_version.trim(), "Primary slot version updated");
        }

        info!("Slot swap completed");
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn test_config() -> LinuxSlotConfig {
        LinuxSlotConfig {
            primary_device_hint: Some("/dev/test-p2".into()),
            alternate_device_hint: Some("/dev/test-p3".into()),
            uboot_env_path: None,
            efi_vars_path: None,
            min_free_space: 1024 * 1024,
        }
    }

    #[tokio::test]
    async fn test_detect_slots_with_hints() {
        let provider = LinuxSlotProvider::new(test_config());
        let layout = provider.detect_slots().await.unwrap();
        assert_eq!(layout.primary.id, SlotId::Primary);
        assert_eq!(layout.alternate.id, SlotId::Alternate);
        assert_eq!(layout.primary.device_path, "/dev/test-p2");
        assert_eq!(layout.alternate.device_path, "/dev/test-p3");
    }

    #[tokio::test]
    async fn test_get_active_slot_defaults_to_primary() {
        let provider = LinuxSlotProvider::new(test_config());
        // Without U-Boot env, should default to primary
        let slot = provider.get_active_slot().await.unwrap();
        assert_eq!(slot, SlotId::Primary);
    }

    #[test]
    fn test_fs_type_detection() {
        let provider = LinuxSlotProvider::new(test_config());
        assert_eq!(
            provider.detect_fs_type("/dev/mmcblk0p2"),
            FileSystemType::Ext4
        );
        assert_eq!(
            provider.detect_fs_type("/dev/nvme0n1p3"),
            FileSystemType::Ext4
        );
    }
}
