//! Hardware-derived device identity.
//!
//! SystemIdentity is a deterministic fingerprint of the physical device.
//! The same device always produces the same identity across reboots.

use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use tracing::warn;

/// SystemIdentity is the hardware-derived fingerprint of a device.
///
/// The identity is deterministic — the same physical device
/// always produces the same identity string.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct SystemIdentity {
    /// Machine ID from /etc/machine-id or DMI product_uuid.
    pub machine_id: String,
    /// Primary MAC address.
    pub mac_address: String,
    /// Device serial number (from DMI).
    pub serial: Option<String>,
    /// Board / product model (from DMI).
    pub board_model: Option<String>,
    /// Kernel version at attestation-time.
    pub kernel_version: String,
}

impl SystemIdentity {
    /// Deterministic identity key suitable for use as a device ID.
    pub fn identity_key(&self) -> String {
        // Concatenate stable fields — machine_id and mac should never change.
        format!(
            "vela:{}:{}",
            self.machine_id,
            &self.mac_address[..self.mac_address.len().min(12)]
        )
    }
}

/// Identity provider for Linux systems.
///
/// Reads from /etc/machine-id, /sys/class/net/*/address,
/// and /sys/class/dmi/id/*.
pub struct LinuxIdentityProvider {
    machine_id_path: PathBuf,
    net_sys_path: PathBuf,
    dmi_sys_path: PathBuf,
}

impl Default for LinuxIdentityProvider {
    fn default() -> Self {
        Self {
            machine_id_path: PathBuf::from("/etc/machine-id"),
            net_sys_path: PathBuf::from("/sys/class/net"),
            dmi_sys_path: PathBuf::from("/sys/class/dmi/id"),
        }
    }
}

impl LinuxIdentityProvider {
    /// Create a provider with custom paths (for testing).
    pub fn new(machine_id_path: PathBuf, net_sys_path: PathBuf, dmi_sys_path: PathBuf) -> Self {
        Self {
            machine_id_path,
            net_sys_path,
            dmi_sys_path,
        }
    }

    /// Read /etc/machine-id, stripping whitespace.
    fn read_machine_id(&self) -> Option<String> {
        std::fs::read_to_string(&self.machine_id_path)
            .ok()
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty())
    }

    /// Read the first non-loopback MAC address.
    fn read_mac_address(&self) -> Option<String> {
        let entries = std::fs::read_dir(&self.net_sys_path).ok()?;
        for entry in entries.filter_map(|e| e.ok()) {
            let name = entry.file_name().to_string_lossy().to_string();
            if name == "lo" {
                continue;
            }
            let addr_path = entry.path().join("address");
            if let Ok(addr) = std::fs::read_to_string(&addr_path) {
                let addr = addr.trim().to_string();
                if !addr.is_empty() && addr != "00:00:00:00:00:00" {
                    return Some(addr);
                }
            }
        }
        None
    }

    /// Read DMI product serial.
    fn read_dmi_serial(&self) -> Option<String> {
        std::fs::read_to_string(self.dmi_sys_path.join("product_serial"))
            .ok()
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty())
    }

    /// Read DMI board model.
    fn read_dmi_board(&self) -> Option<String> {
        let product = std::fs::read_to_string(self.dmi_sys_path.join("product_name"))
            .ok()
            .map(|s| s.trim().to_string());

        let board = std::fs::read_to_string(self.dmi_sys_path.join("board_name"))
            .ok()
            .map(|s| s.trim().to_string());

        match (product, board) {
            (Some(p), Some(b)) => Some(format!("{} / {}", p, b)),
            (Some(p), None) => Some(p),
            (None, Some(b)) => Some(b),
            (None, None) => None,
        }
    }

    /// Read kernel version from /proc/version.
    fn read_kernel_version(&self) -> String {
        std::fs::read_to_string("/proc/version")
            .unwrap_or_else(|_| "unknown".into())
            .split_whitespace()
            .take(3)
            .collect::<Vec<_>>()
            .join(" ")
    }

    /// Build a complete SystemIdentity from the running system.
    pub fn collect(&self) -> Option<SystemIdentity> {
        let machine_id = self.read_machine_id()?;
        let mac_address = self.read_mac_address().unwrap_or_else(|| "unknown".into());
        let serial = self.read_dmi_serial();
        let board_model = self.read_dmi_board();
        let kernel_version = self.read_kernel_version();

        Some(SystemIdentity {
            machine_id,
            mac_address,
            serial,
            board_model,
            kernel_version,
        })
    }

    /// Try to collect, returning a warning if any field is missing.
    pub fn collect_or_warn(&self) -> SystemIdentity {
        match self.collect() {
            Some(id) => id,
            None => {
                warn!("Failed to collect system identity — using fallback");
                SystemIdentity {
                    machine_id: "fallback-unknown".into(),
                    mac_address: "00:00:00:00:00:00".into(),
                    serial: None,
                    board_model: None,
                    kernel_version: self.read_kernel_version(),
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    fn temp_dir() -> tempfile::TempDir {
        tempfile::tempdir().expect("tempdir")
    }

    fn write_file(dir: &std::path::Path, name: &str, content: &str) {
        let path = dir.join(name);
        let mut f = std::fs::File::create(&path).expect("create");
        f.write_all(content.as_bytes()).expect("write");
    }

    #[test]
    fn test_identity_key_deterministic() {
        let id = SystemIdentity {
            machine_id: "abc123".into(),
            mac_address: "aa:bb:cc:dd:ee:ff".into(),
            serial: Some("SN001".into()),
            board_model: Some("TestBoard".into()),
            kernel_version: "Linux 5.10".into(),
        };
        assert_eq!(id.identity_key(), "vela:abc123:aa:bb:cc:dd:");
    }

    #[test]
    fn test_collect_from_mock_filesystem() {
        let dir = temp_dir();
        write_file(dir.path(), "machine-id", "test-machine-id\n");
        let net = dir.path().join("net");
        std::fs::create_dir_all(net.join("eth0")).unwrap();
        write_file(&net.join("eth0"), "address", "11:22:33:44:55:66\n");
        // Add a loopback entry that should be skipped
        std::fs::create_dir_all(net.join("lo")).unwrap();
        write_file(&net.join("lo"), "address", "00:00:00:00:00:00\n");
        write_file(dir.path(), "product_serial", "TESTSN001\n");
        write_file(dir.path(), "product_name", "TestBoard\n");
        write_file(dir.path(), "board_name", "RevA\n");

        let provider = LinuxIdentityProvider::new(
            dir.path().join("machine-id"),
            net,
            dir.path().to_path_buf(),
        );

        let id = provider.collect().expect("should collect identity");
        assert_eq!(id.machine_id, "test-machine-id");
        assert_eq!(id.mac_address, "11:22:33:44:55:66");
        assert_eq!(id.serial.as_deref(), Some("TESTSN001"));
        assert!(id.board_model.as_deref().unwrap().contains("TestBoard"));
    }

    #[test]
    fn test_collect_fallback_when_no_machine_id() {
        let dir = temp_dir();
        let provider = LinuxIdentityProvider::new(
            dir.path().join("machine-id"),
            dir.path().to_path_buf(),
            dir.path().to_path_buf(),
        );
        let id = provider.collect_or_warn();
        assert!(id.machine_id.contains("fallback"));
    }
}
