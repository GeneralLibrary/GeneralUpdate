//! Delta manifest — metadata for delta update bundles.
//!
//! Stored as `delta.manifest` inside the .fpk tar archive.
//! The manifest describes the baseline version requirement and
//! integrity constraints for applying the delta patch.

use serde::{Deserialize, Serialize};

/// Delta manifest stored alongside the binary patch in a FlashPack.
///
/// The manifest ensures the device has the correct baseline version
/// before attempting to apply an incremental update.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DeltaManifest {
    /// Version of the delta format (e.g. "1.0.0").
    pub format_version: String,

    /// Human-readable name for this delta bundle.
    pub bundle_name: String,

    /// Target version after applying this delta.
    pub target_version: String,

    /// Exact baseline version required on the device.
    /// The device MUST be running this version before applying.
    pub requires_version: String,

    /// SHA-256 of the baseline firmware file.
    pub base_hash: String,

    /// SHA-256 of the target firmware file (after patching).
    pub target_hash: String,

    /// Expected size of the target firmware in bytes.
    pub target_size: u64,

    /// Size of the delta patch file in bytes.
    pub delta_size: u64,

    /// Compression ratio (delta_size / target_size).
    /// For informational purposes — smaller is better.
    pub compression_ratio: f64,

    /// ISO-8601 timestamp of delta creation.
    pub created_at: String,

    /// Identifier of the builder or CI pipeline.
    pub builder_id: String,
}

impl DeltaManifest {
    /// Create a new manifest from diff results.
    pub fn new(
        bundle_name: String,
        target_version: String,
        requires_version: String,
        base_hash: String,
        target_hash: String,
        target_size: u64,
        delta_size: u64,
    ) -> Self {
        let compression_ratio = if target_size > 0 {
            delta_size as f64 / target_size as f64
        } else {
            1.0
        };

        Self {
            format_version: "1.0.0".into(),
            bundle_name,
            target_version,
            requires_version,
            base_hash,
            target_hash,
            target_size,
            delta_size,
            compression_ratio,
            created_at: chrono::Utc::now().to_rfc3339(),
            builder_id: "vela-delta-engine".into(),
        }
    }

    /// Validate that the device's current version matches the required baseline.
    pub fn validate_baseline(
        &self,
        device_version: &str,
    ) -> Result<(), String> {
        if device_version != self.requires_version {
            return Err(format!(
                "Baseline version mismatch: device has {}, delta requires {}",
                device_version, self.requires_version
            ));
        }
        Ok(())
    }

    /// Check if this delta is reasonably sized (not larger than full update).
    pub fn is_efficient(&self) -> bool {
        self.compression_ratio < 0.95
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_manifest_serialization() {
        let manifest = DeltaManifest::new(
            "vela-os-v2.1.3".into(),
            "2.1.3".into(),
            "2.1.2".into(),
            "abc123".into(),
            "def456".into(),
            1048576,
            102400,
        );

        let json = serde_json::to_string(&manifest).unwrap();
        let decoded: DeltaManifest = serde_json::from_str(&json).unwrap();

        assert_eq!(decoded.bundle_name, "vela-os-v2.1.3");
        assert_eq!(decoded.target_version, "2.1.3");
        assert_eq!(decoded.requires_version, "2.1.2");
        assert_eq!(decoded.format_version, "1.0.0");
        assert!(decoded.compression_ratio < 0.5);
    }

    #[test]
    fn test_validate_baseline_match() {
        let m = DeltaManifest::new(
            "test".into(), "2.0".into(), "1.0".into(),
            "a".into(), "b".into(), 100, 50,
        );
        assert!(m.validate_baseline("1.0").is_ok());
    }

    #[test]
    fn test_validate_baseline_mismatch() {
        let m = DeltaManifest::new(
            "test".into(), "2.0".into(), "1.0".into(),
            "a".into(), "b".into(), 100, 50,
        );
        assert!(m.validate_baseline("0.9").is_err());
    }

    #[test]
    fn test_efficiency_check() {
        let efficient = DeltaManifest::new(
            "test".into(), "2.0".into(), "1.0".into(),
            "a".into(), "b".into(), 10000, 1000,
        );
        assert!(efficient.is_efficient()); // 10% ratio

        let inefficient = DeltaManifest::new(
            "test".into(), "2.0".into(), "1.0".into(),
            "a".into(), "b".into(), 1000, 999,
        );
        assert!(!inefficient.is_efficient()); // 99.9% ratio
    }
}
