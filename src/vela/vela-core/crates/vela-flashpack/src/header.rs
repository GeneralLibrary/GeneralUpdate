//! FlashPack header types and parsing.
//!
//! Defines the `FpkHeader` struct that sits inside `fpk-header.json`
//! at the root of every `.fpk` tar archive.

use serde::{Deserialize, Serialize};

use crate::{FlashPackError, FpkResult};

/// Payload type classification for an update bundle.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum PayloadType {
    /// Full rootfs image — replaces the entire alternate slot.
    FullImage,
    /// Delta (binary diff) from a known baseline version.
    Delta,
    /// Application-layer update (e.g. container, config).
    Application,
}

impl std::fmt::Display for PayloadType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::FullImage => write!(f, "full_image"),
            Self::Delta => write!(f, "delta"),
            Self::Application => write!(f, "application"),
        }
    }
}

/// FlashPack bundle header stored in `fpk-header.json`.
///
/// This is the first file read from the `.fpk` tar archive and
/// describes the bundle contents, compatibility, and integrity
/// expectations.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FpkHeader {
    /// Semantic version of the FlashPack format used (e.g. "1.0.0").
    pub format_version: String,
    /// Minimum reader version required to parse this bundle.
    /// Readers with a lower `format_version` must refuse the bundle.
    pub min_reader_version: String,
    /// Human-readable bundle name (e.g. "vela-os-v2.1.3").
    pub bundle_name: String,
    /// Semantic version of this bundle payload.
    pub bundle_version: String,
    /// Hardware slot models this bundle is compatible with.
    pub compatible_slots: Vec<String>,
    /// Classification of the enclosed payload.
    pub payload_type: PayloadType,
    /// Expected payload size in bytes (used for size validation).
    pub payload_size: u64,
    /// Minimum version that must already be installed before applying.
    pub requires_version: String,
    /// ISO-8601 timestamp of bundle creation.
    pub created_at: String,
    /// Identifier of the builder (CI pipeline ID, developer name, etc.).
    pub builder_id: String,
    /// Forward-compatible feature flags (e.g. "streaming_verify").
    /// Readers ignore flags they do not recognise.
    #[serde(default)]
    pub compat_flags: Vec<String>,
}

/// A parsed semantic version (major.minor.patch).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SemVer {
    pub major: u32,
    pub minor: u32,
    pub patch: u32,
}

impl std::fmt::Display for SemVer {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}.{}.{}", self.major, self.minor, self.patch)
    }
}

impl std::str::FromStr for SemVer {
    type Err = FlashPackError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let parts: Vec<&str> = s.trim().split('.').collect();
        if parts.len() != 3 {
            return Err(FlashPackError::InvalidFormat(format!(
                "Invalid SemVer '{s}': expected MAJOR.MINOR.PATCH"
            )));
        }
        let major = parts[0]
            .parse::<u32>()
            .map_err(|_| FlashPackError::InvalidFormat(format!("Invalid major version in '{s}'")))?;
        let minor = parts[1]
            .parse::<u32>()
            .map_err(|_| FlashPackError::InvalidFormat(format!("Invalid minor version in '{s}'")))?;
        let patch = parts[2]
            .parse::<u32>()
            .map_err(|_| FlashPackError::InvalidFormat(format!("Invalid patch version in '{s}'")))?;
        Ok(Self { major, minor, patch })
    }
}

impl PartialOrd for SemVer {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl Ord for SemVer {
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        self.major
            .cmp(&other.major)
            .then(self.minor.cmp(&other.minor))
            .then(self.patch.cmp(&other.patch))
    }
}

impl FpkHeader {
    /// Parse the header from JSON bytes.
    pub fn from_json(data: &[u8]) -> FpkResult<Self> {
        let header: Self = serde_json::from_slice(data)?;
        header.validate()?;
        Ok(header)
    }

    /// Validate internal consistency of the header fields.
    pub fn validate(&self) -> FpkResult<()> {
        if self.bundle_name.is_empty() {
            return Err(FlashPackError::InvalidFormat(
                "bundle_name must not be empty".into(),
            ));
        }
        // Validate format_version is a valid SemVer
        let _fv: SemVer = self.format_version.parse()?;
        // Validate min_reader_version
        let _mrv: SemVer = self.min_reader_version.parse()?;
        // Validate bundle_version
        let _bv: SemVer = self.bundle_version.parse()?;
        // Validate requires_version
        let _rv: SemVer = self.requires_version.parse()?;
        Ok(())
    }

    /// Check whether a reader at `reader_version` can read this header.
    ///
    /// Compatibility rules:
    /// - Same major and minor → compatible (patch bumps are always compatible).
    /// - Newer minor in header → reader can read it (forward-compatible flag fields).
    /// - Different major → incompatible; reader must refuse.
    pub fn is_reader_compatible(&self, reader_version: &SemVer) -> bool {
        let Ok(min_reader) = self.min_reader_version.parse::<SemVer>() else {
            return false;
        };
        // Reader must be at least min_reader_version
        if reader_version < &min_reader {
            return false;
        }
        // Major version must match
        let Ok(format_ver) = self.format_version.parse::<SemVer>() else {
            return false;
        };
        format_ver.major == reader_version.major
    }

    /// Check whether the given `current_version` satisfies the `requires_version` constraint.
    pub fn check_version_requirement(&self, current_version: &SemVer) -> FpkResult<()> {
        let required: SemVer = self.requires_version.parse()?;
        if current_version < &required {
            return Err(FlashPackError::VersionTooLow {
                current: current_version.to_string(),
                required: required.to_string(),
            });
        }
        Ok(())
    }

    /// Serialize the header to JSON bytes.
    pub fn to_json(&self) -> FpkResult<Vec<u8>> {
        Ok(serde_json::to_vec_pretty(self)?)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample_header() -> FpkHeader {
        FpkHeader {
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            bundle_name: "vela-os-v2.1.3".into(),
            bundle_version: "2.1.3".into(),
            compatible_slots: vec!["rpi4-model-b".into()],
            payload_type: PayloadType::FullImage,
            payload_size: 1024 * 1024 * 50,
            requires_version: "2.0.0".into(),
            created_at: "2026-05-18T12:00:00Z".into(),
            builder_id: "ci/v0.1".into(),
            compat_flags: vec!["streaming_verify".into()],
        }
    }

    #[test]
    fn test_semver_parse_and_compare() {
        let v1: SemVer = "1.0.0".parse().unwrap();
        let v2: SemVer = "1.0.1".parse().unwrap();
        let v3: SemVer = "2.0.0".parse().unwrap();
        assert!(v1 < v2);
        assert!(v2 < v3);
        assert_eq!(v1.to_string(), "1.0.0");
    }

    #[test]
    fn test_header_serialization_roundtrip() {
        let header = sample_header();
        let json = header.to_json().unwrap();
        let parsed = FpkHeader::from_json(&json).unwrap();
        assert_eq!(header.bundle_name, parsed.bundle_name);
        assert_eq!(header.bundle_version, parsed.bundle_version);
    }

    #[test]
    fn test_header_validate_empty_name() {
        let mut header = sample_header();
        header.bundle_name = "".into();
        assert!(header.validate().is_err());
    }

    #[test]
    fn test_reader_compatibility() {
        let header = sample_header();
        let reader_v1: SemVer = "1.0.0".parse().unwrap();
        let reader_v1_1: SemVer = "1.0.1".parse().unwrap();
        let reader_v2: SemVer = "2.0.0".parse().unwrap();

        assert!(header.is_reader_compatible(&reader_v1));
        assert!(header.is_reader_compatible(&reader_v1_1));
        // Different major version → not compatible
        assert!(!header.is_reader_compatible(&reader_v2));
    }

    #[test]
    fn test_version_requirement_met() {
        let header = sample_header(); // requires 2.0.0
        let current: SemVer = "2.1.0".parse().unwrap();
        assert!(header.check_version_requirement(&current).is_ok());
    }

    #[test]
    fn test_version_requirement_too_low() {
        let header = sample_header(); // requires 2.0.0
        let current: SemVer = "1.9.0".parse().unwrap();
        assert!(header.check_version_requirement(&current).is_err());
    }
}
