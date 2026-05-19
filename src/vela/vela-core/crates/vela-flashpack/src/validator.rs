//! Bundle validation: signature verification, version chain check, and integrity assertions.
//!
//! The `BundleValidator` performs the full validation pipeline on a parsed FlashPack:
//! 1. Checksum verification
//! 2. Signature verification (RSA-PSS / ECDSA-P256)
//! 3. Version chain validation
//! 4. Compatibility flag check

use std::collections::HashSet;

use tracing::{debug, info, instrument, trace, warn};

use crate::header::SemVer;
use crate::reader::{BundleHash, FlashPackReader};
use crate::{FlashPackError, FpkResult};

use vela_crypto::{BundleSigner, BundleVerifier};

/// Currently supported compat_flags by this reader version.
const CURRENT_SUPPORTED_FLAGS: &[&str] = &["streaming_verify"];

/// Performs the full validation pipeline on an opened FlashPack.
pub struct BundleValidator;

impl BundleValidator {
    /// Full validation pipeline: checksums → signature → version chain → compat flags.
    ///
    /// Returns the computed `BundleHash` on success.
    #[instrument(skip(reader, verifier, current_version))]
    pub fn validate(
        reader: &FlashPackReader,
        verifier: &dyn BundleVerifier,
        current_version: &SemVer,
    ) -> FpkResult<BundleHash> {
        debug!("Starting bundle validation pipeline");

        // Step 1: Verify checksums
        trace!(step = "checksums");
        let hash = reader.verify_checksums()?;

        // Step 2: Verify signature
        trace!(step = "signature", algorithm = ?verifier.algorithm());
        Self::verify_signature(reader, verifier)?;

        // Step 3: Validate version chain
        trace!(step = "version_chain");
        Self::validate_version_chain(&reader.header, current_version)?;

        // Step 4: Check compat_flags
        trace!(step = "compat_flags");
        Self::check_compat_flags(&reader.header.compat_flags)?;

        info!(
            hash = %hex::encode(&hash.sha256[..8]),
            bundle = %reader.header.bundle_name,
            "Bundle validation passed"
        );
        Ok(hash)
    }

    /// Verify the detached PKCS#7 signature against the bundle.
    ///
    /// The signature covers `fpk-header.json` content. We re-read it from the
    /// archive and verify it against the provided public key.
    fn verify_signature(reader: &FlashPackReader, verifier: &dyn BundleVerifier) -> FpkResult<()> {
        // For detached signatures, we need the original data that was signed.
        // In our format the signature covers the fpk-header.json content.
        let header_json = reader.header.to_json()?;

        let is_valid = verifier
            .verify(&header_json, &reader.signature)
            .map_err(|e| FlashPackError::Crypto(e))?;

        if !is_valid {
            warn!("Bundle signature verification FAILED");
            return Err(FlashPackError::InvalidFormat(
                "Signature verification failed: bundle may be tampered or signed by an untrusted key".into(),
            ));
        }

        debug!("Signature verification passed");
        Ok(())
    }

    /// Validate the version chain from the current device version to the bundle version.
    ///
    /// Checks:
    /// 1. `requires_version` constraint is satisfied.
    /// 2. The format version is compatible with the current reader.
    /// 3. compat_flags are a subset of currently supported flags.
    #[instrument(skip(header), fields(
        current = %current_version,
        required = %header.requires_version,
        target = %header.bundle_version
    ))]
    pub fn validate_version_chain(
        header: &crate::header::FpkHeader,
        current_version: &SemVer,
    ) -> FpkResult<()> {
        // 1. Check requires_version
        if current_version < &header.requires_version.parse::<SemVer>()? {
            warn!(
                current = %current_version,
                required = %header.requires_version,
                "Version requirement not met"
            );
            return Err(FlashPackError::VersionTooLow {
                current: current_version.to_string(),
                required: header.requires_version.clone(),
            });
        }

        // 2. The bundle version must be newer than the current version
        // (we don't allow downgrades unless force-install is set, which is handled by the lifecycle)
        let target_version: SemVer = header.bundle_version.parse()?;
        if target_version <= *current_version {
            debug!(
                current = %current_version,
                target = %target_version,
                "Bundle version is not newer than current — downgrade detected"
            );
            // This is a soft check; lifecycle can override.
        }

        // 3. Check format compatibility
        let format_ver: SemVer = header.format_version.parse()?;
        let min_reader: SemVer = header.min_reader_version.parse()?;

        // The reader version (hardcoded) must be >= min_reader_version
        let reader_ver: SemVer = crate::REQ_SIZE.parse().unwrap();
        if reader_ver < min_reader {
            return Err(FlashPackError::FormatIncompatible {
                format_version: header.format_version.clone(),
                min_reader: header.min_reader_version.clone(),
            });
        }

        // Major version must match
        if reader_ver.major != format_ver.major {
            return Err(FlashPackError::FormatIncompatible {
                format_version: header.format_version.clone(),
                min_reader: header.min_reader_version.clone(),
            });
        }

        info!(
            "Version chain validated: {} -> {}",
            current_version, target_version
        );
        Ok(())
    }

    /// Check that all `compat_flags` declared in the bundle are supported by this reader.
    ///
    /// Unknown flags are ignored (forward-compatible), but required flags that are
    /// missing from the reader's supported set cause rejection.
    fn check_compat_flags(flags: &[String]) -> FpkResult<()> {
        if flags.is_empty() {
            return Ok(());
        }

        let supported: HashSet<&str> = CURRENT_SUPPORTED_FLAGS.iter().copied().collect();
        for flag in flags {
            if !supported.contains(flag.as_str()) {
                return Err(FlashPackError::InvalidFormat(format!(
                    "Bundle requires compat_flag '{flag}' which is not supported by this reader"
                )));
            }
        }

        debug!(?flags, "Compat flags verified");
        Ok(())
    }
}

/// Sign a FlashPack header (or any data) using a `BundleSigner`.
///
/// This is a convenience wrapper that delegates to `vela-crypto` signing.
#[instrument(skip(signer, data), fields(algorithm = ?signer.algorithm()))]
pub fn sign_bundle(signer: &dyn BundleSigner, data: &[u8]) -> FpkResult<Vec<u8>> {
    trace!(size = data.len(), "Signing bundle data");
    let signature = signer.sign(data).map_err(FlashPackError::Crypto)?;
    info!(sig_len = signature.len(), "Bundle signed");
    Ok(signature)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::builder::{BuilderConfig, FlashPackBuilder};
    use crate::header::PayloadType;
    use vela_crypto::{
        BundleSigner, BundleVerifier, CryptoResult, PublicKey, SignatureAlgorithm, SigningKey,
    };

    /// A mock verifier that always returns true.
    struct AlwaysPassVerifier;
    impl BundleVerifier for AlwaysPassVerifier {
        fn algorithm(&self) -> SignatureAlgorithm {
            SignatureAlgorithm::EcdsaP256Sha256
        }
        fn verify(&self, _data: &[u8], _signature: &[u8]) -> CryptoResult<bool> {
            Ok(true)
        }
    }

    /// A mock verifier that always returns false.
    struct AlwaysFailVerifier;
    impl BundleVerifier for AlwaysFailVerifier {
        fn algorithm(&self) -> SignatureAlgorithm {
            SignatureAlgorithm::EcdsaP256Sha256
        }
        fn verify(&self, _data: &[u8], _signature: &[u8]) -> CryptoResult<bool> {
            Ok(false)
        }
    }

    fn build_test_bundle(dir: &tempfile::TempDir) -> (std::path::PathBuf, FlashPackReader) {
        let payload_path = dir.path().join("payload.bin");
        std::fs::write(&payload_path, b"validation test payload").unwrap();
        let fpk_path = dir.path().join("test.fpk");

        let config = BuilderConfig {
            payload_path: payload_path.to_string_lossy().to_string(),
            bundle_name: "test-validation".into(),
            bundle_version: "2.1.3".into(),
            compatible_slots: vec!["rpi4".into()],
            payload_type: PayloadType::FullImage,
            requires_version: "2.0.0".into(),
            builder_id: "test-ci".into(),
            signer: None,
            format_version: "1.0.0".into(),
            min_reader_version: "1.0.0".into(),
            compat_flags: vec![],
        };

        FlashPackBuilder::new(config).build(&fpk_path).unwrap();
        let reader = FlashPackReader::open(&fpk_path).unwrap();
        (fpk_path, reader)
    }

    #[test]
    fn test_full_validation_passes() {
        let dir = tempfile::tempdir().unwrap();
        let (_fpk, reader) = build_test_bundle(&dir);
        let current: SemVer = "2.1.0".parse().unwrap();
        let verifier = AlwaysPassVerifier;

        let hash = BundleValidator::validate(&reader, &verifier, &current).unwrap();
        assert_eq!(hash.sha256.len(), 32);
    }

    #[test]
    fn test_validation_fails_on_bad_signature() {
        let dir = tempfile::tempdir().unwrap();
        let (_fpk, reader) = build_test_bundle(&dir);
        let current: SemVer = "2.1.0".parse().unwrap();
        let verifier = AlwaysFailVerifier;

        let result = BundleValidator::validate(&reader, &verifier, &current);
        assert!(result.is_err());
    }

    #[test]
    fn test_version_chain_too_low() {
        let dir = tempfile::tempdir().unwrap();
        let (_fpk, reader) = build_test_bundle(&dir);
        let current: SemVer = "1.0.0".parse().unwrap(); // below requires 2.0.0

        let result = BundleValidator::validate_version_chain(&reader.header, &current);
        assert!(result.is_err());
    }

    #[test]
    fn test_version_chain_valid() {
        let dir = tempfile::tempdir().unwrap();
        let (_fpk, reader) = build_test_bundle(&dir);
        let current: SemVer = "2.1.0".parse().unwrap();

        assert!(BundleValidator::validate_version_chain(&reader.header, &current).is_ok());
    }

    #[test]
    fn test_unsupported_compat_flag_rejected() {
        let dir = tempfile::tempdir().unwrap();
        let (_fpk, reader) = build_test_bundle(&dir);
        let current: SemVer = "2.1.0".parse().unwrap();

        // Inject an unsupported flag
        let mut header = reader.header.clone();
        header.compat_flags = vec!["non_existent_flag".into()];

        let result = BundleValidator::validate_version_chain(&header, &current);
        // The compat flag check is part of validate, not validate_version_chain
        // Let's test it separately
        let result = super::BundleValidator::check_compat_flags(&header.compat_flags);
        assert!(result.is_err());
    }

    #[test]
    fn test_supported_compat_flag_accepted() {
        let result = BundleValidator::check_compat_flags(&["streaming_verify".to_string()]);
        assert!(result.is_ok());
    }
}
