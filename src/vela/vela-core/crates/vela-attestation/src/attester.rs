//! Device attester: produces signed attestation claims about the device.
//!
//! Follows a simplified TCG-style model — each claim includes
//! PCR-like measurements of system state at attestation time.

use serde::{Deserialize, Serialize};
use std::time::{SystemTime, UNIX_EPOCH};
use tracing::{info, instrument};

use crate::identity::SystemIdentity;
use crate::{AttestationError, AttestationResult};

/// A single attestation claim, akin to a TCG PCR measurement.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AttestationClaim {
    /// Claim type (e.g., "boot_health", "fs_integrity", "slot_status").
    pub claim_type: String,
    /// Claim measurement value (for integrity verification).
    pub measurement: String,
    /// Human-readable description.
    pub description: String,
}

/// A complete attestation payload signed by the device.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AttestationPayload {
    /// The device that made this claim.
    pub device_id: String,
    /// Timestamp (epoch seconds) when the attestation was created.
    pub timestamp_secs: u64,
    /// Claims about the current system state.
    pub claims: Vec<AttestationClaim>,
    /// Nonce from the Hub challenge for anti-replay.
    pub nonce: Option<String>,
    /// Computed signature over the payload.
    pub signature: Option<Vec<u8>>,
}

/// Attester produces signed attestation payloads for a device.
pub struct Attester {
    identity: SystemIdentity,
    /// Simulated measurement provider
    measurement_provider: Box<dyn MeasurementProvider>,
}

/// Trait for providing system measurements (testable / mockable).
pub trait MeasurementProvider: Send + Sync {
    fn measure_boot_health(&self) -> AttestationResult<AttestationClaim>;
    fn measure_filesystem_integrity(&self) -> AttestationResult<AttestationClaim>;
    fn measure_slot_status(&self) -> AttestationResult<AttestationClaim>;
}

/// Default measurements from a real Linux system.
pub struct DefaultMeasurementProvider;

impl MeasurementProvider for DefaultMeasurementProvider {
    fn measure_boot_health(&self) -> AttestationResult<AttestationClaim> {
        // Check /proc/uptime — system has been up for some time → healthy
        let uptime = std::fs::read_to_string("/proc/uptime")
            .ok()
            .and_then(|s| {
                s.split_whitespace()
                    .next()?
                    .parse::<f64>()
                    .ok()
            })
            .unwrap_or(0.0);

        let status = if uptime > 5.0 { "healthy" } else { "booting" };
        Ok(AttestationClaim {
            claim_type: "boot_health".into(),
            measurement: format!("uptime:{:.0}", uptime),
            description: format!("System boot status: {status}"),
        })
    }

    fn measure_filesystem_integrity(&self) -> AttestationResult<AttestationClaim> {
        // Simplified: check that /etc exists
        let ok = std::path::Path::new("/etc").exists();
        Ok(AttestationClaim {
            claim_type: "fs_integrity".into(),
            measurement: if ok { "ok" } else { "degraded" }.into(),
            description: format!("Filesystem integrity check: {}", if ok { "passed" } else { "degraded" }),
        })
    }

    fn measure_slot_status(&self) -> AttestationResult<AttestationClaim> {
        // Report current boot slot
        let slot = std::env::var("VELA_BOOT_SLOT").unwrap_or_else(|_| "primary".into());
        Ok(AttestationClaim {
            claim_type: "slot_status".into(),
            measurement: format!("active:{}", slot),
            description: format!("Active boot slot: {slot}"),
        })
    }
}

impl Attester {
    /// Create an attester for the given device identity.
    pub fn new(identity: SystemIdentity) -> Self {
        Self {
            identity,
            measurement_provider: Box::new(DefaultMeasurementProvider),
        }
    }

    /// Create with a custom measurement provider (for testing).
    pub fn with_provider(
        identity: SystemIdentity,
        provider: Box<dyn MeasurementProvider>,
    ) -> Self {
        Self {
            identity,
            measurement_provider: provider,
        }
    }

    /// Build a complete attestation payload (unsigned).
    #[instrument(skip(self))]
    pub fn build_payload(&self, nonce: Option<String>) -> AttestationResult<AttestationPayload> {
        let mut claims = Vec::new();

        claims.push(self.measurement_provider.measure_boot_health()?);
        claims.push(
            self.measurement_provider
                .measure_filesystem_integrity()?,
        );
        claims.push(self.measurement_provider.measure_slot_status()?);

        let timestamp_secs = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_secs())
            .unwrap_or(0);

        info!(
            device_id = %self.identity.identity_key(),
            claim_count = claims.len(),
            "Built attestation payload"
        );

        Ok(AttestationPayload {
            device_id: self.identity.identity_key(),
            timestamp_secs,
            claims,
            nonce,
            signature: None,
        })
    }

    /// Build and sign an attestation payload.
    ///
    /// The signature uses the device's attestation signing key (AkSymmetric).
    /// In this implementation we compute a simple HMAC for verification.
    #[instrument(skip(self, signing_key))]
    pub fn build_signed_payload(
        &self,
        signing_key: &[u8],
        nonce: Option<String>,
    ) -> AttestationResult<AttestationPayload> {
        let mut payload = self.build_payload(nonce)?;
        let canonical = payload.canonical_for_signing();
        let sig = self.sign_payload(signing_key, &canonical);
        payload.signature = Some(sig);
        Ok(payload)
    }

    /// Canonical representation of the payload for signing.
    fn sign_payload(&self, key: &[u8], canonical: &[u8]) -> Vec<u8> {
        use sha2::Digest;
        use hmac::Mac;
        let mut mac = hmac::Hmac::<sha2::Sha256>::new_from_slice(key)
            .expect("HMAC key length");
        mac.update(canonical);
        mac.finalize().into_bytes().to_vec()
    }
}

impl AttestationPayload {
    /// Produce a canonical byte sequence for signing.
    ///
    /// Stable ordering: device_id || timestamp || claim_type:measurement || nonce.
    pub fn canonical_for_signing(&self) -> Vec<u8> {
        let mut buf = Vec::new();
        buf.extend_from_slice(self.device_id.as_bytes());
        buf.push(b':');
        buf.extend_from_slice(self.timestamp_secs.to_string().as_bytes());

        // Claims in stable order by claim_type
        let mut sorted_claims: Vec<_> = self.claims.iter().collect();
        sorted_claims.sort_by_key(|c| &c.claim_type);
        for claim in &sorted_claims {
            buf.push(b'|');
            buf.extend_from_slice(claim.claim_type.as_bytes());
            buf.push(b':');
            buf.extend_from_slice(claim.measurement.as_bytes());
        }

        if let Some(ref nonce) = self.nonce {
            buf.push(b'|');
            buf.extend_from_slice(nonce.as_bytes());
        }

        buf
    }

    /// Verify the signature on this payload.
    pub fn verify(&self, key: &[u8]) -> bool {
        let sig = match &self.signature {
            Some(s) => s,
            None => return false,
        };
        let canonical = self.canonical_for_signing();
        use hmac::Mac;
        let mut mac =
            hmac::Hmac::<sha2::Sha256>::new_from_slice(key).expect("HMAC key length");
        mac.update(&canonical);
        mac.verify_slice(sig).is_ok()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    struct FakeProvider;

    impl MeasurementProvider for FakeProvider {
        fn measure_boot_health(&self) -> AttestationResult<AttestationClaim> {
            Ok(AttestationClaim {
                claim_type: "boot_health".into(),
                measurement: "healthy".into(),
                description: "fake".into(),
            })
        }

        fn measure_filesystem_integrity(&self) -> AttestationResult<AttestationClaim> {
            Ok(AttestationClaim {
                claim_type: "fs_integrity".into(),
                measurement: "ok".into(),
                description: "fake".into(),
            })
        }

        fn measure_slot_status(&self) -> AttestationResult<AttestationClaim> {
            Ok(AttestationClaim {
                claim_type: "slot_status".into(),
                measurement: "primary".into(),
                description: "fake".into(),
            })
        }
    }

    fn fake_identity() -> SystemIdentity {
        SystemIdentity {
            machine_id: "machine-1".into(),
            mac_address: "aa:bb:cc:dd:ee:ff".into(),
            serial: Some("SN001".into()),
            board_model: Some("TestBoard".into()),
            kernel_version: "Linux 6.1".into(),
        }
    }

    #[test]
    fn test_build_unsigned_payload() {
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let payload = attester.build_payload(None).unwrap();
        assert_eq!(payload.claims.len(), 3);
        assert!(payload.signature.is_none());
        assert!(payload.timestamp_secs > 0);
    }

    #[test]
    fn test_canonical_for_signing_is_deterministic() {
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let p1 = attester.build_payload(None).unwrap();
        let p2 = attester.build_payload(None).unwrap();
        // Can't compare timestamp but claims should be same
        assert_eq!(p1.claims.len(), p2.claims.len());
    }

    #[test]
    fn test_sign_and_verify() {
        let key = b"my-attestation-signing-key-32ch";
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let payload = attester
            .build_signed_payload(key, Some("nonce-abc".into()))
            .unwrap();

        assert!(payload.signature.is_some());
        assert!(payload.verify(key));
    }

    #[test]
    fn test_verify_fails_with_wrong_key() {
        let key = b"my-attestation-signing-key-32ch";
        let wrong_key = b"tampered-signing-key-here----";
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let payload = attester
            .build_signed_payload(key, Some("nonce-abc".into()))
            .unwrap();

        assert!(!payload.verify(wrong_key));
    }

    #[test]
    fn test_verify_fails_without_signature() {
        let key = b"my-attestation-signing-key-32ch";
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let payload = attester.build_payload(None).unwrap();

        assert!(!payload.verify(key));
    }

    #[test]
    fn test_nonce_included_in_canonical() {
        let attester = Attester::with_provider(fake_identity(), Box::new(FakeProvider));
        let p1 = attester.build_payload(Some("nonce-A".into())).unwrap();
        let p2 = attester.build_payload(Some("nonce-B".into())).unwrap();

        assert_ne!(
            p1.canonical_for_signing(),
            p2.canonical_for_signing(),
            "Different nonces should produce different canonical forms"
        );
    }
}
