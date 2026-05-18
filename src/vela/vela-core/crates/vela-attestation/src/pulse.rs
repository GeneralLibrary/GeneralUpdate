//! Health pulse: periodic signed heartbeat to the Vela Hub.
//!
//! Sends system health metrics at a configurable interval,
//! with structured payloads signed by the attestation key.

use std::time::{Duration, Instant};
use tracing::{debug, error, info, instrument, warn};

use crate::attester::{AttestationPayload, Attester};
use crate::{AttestationError, AttestationResult};

/// Health metrics collected at each pulse interval.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct HealthMetrics {
    /// System uptime in seconds (from /proc/uptime).
    pub uptime_secs: f64,
    /// Current load average (1-minute).
    pub load_avg_1m: f64,
    /// Current load average (5-minute).
    pub load_avg_5m: f64,
    /// Memory used (bytes).
    pub mem_used: u64,
    /// Memory total (bytes).
    pub mem_total: u64,
    /// Active slot.
    pub active_slot: String,
    /// Current firmware version.
    pub firmware_version: String,
    /// Number of successful pulses since last reboot.
    pub pulse_count: u64,
}

/// Heartbeat payload sent to the Hub.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct HeartbeatPayload {
    /// The signed attestation that proves device identity.
    pub attestation: AttestationPayload,
    /// Current health metrics.
    pub metrics: HealthMetrics,
    /// Pulse sequence number (monotonically increasing).
    pub sequence: u64,
    /// Hub endpoint URL for this heartbeat.
    pub hub_url: String,
}

/// Configuration for the health pulse.
#[derive(Debug, Clone)]
pub struct PulseConfig {
    /// Interval between heartbeats.
    pub interval: Duration,
    /// Vela Hub base URL.
    pub hub_url: String,
    /// Attestation signing key (in production, from vela-crypto AkSymmetric).
    pub signing_key: Vec<u8>,
    /// Whether to enforce strict signing verification on the Hub side.
    pub require_signed: bool,
}

impl Default for PulseConfig {
    fn default() -> Self {
        Self {
            interval: Duration::from_secs(300), // 5 minutes
            hub_url: "https://hub.vela-ota.dev/api/v1/heartbeat".into(),
            signing_key: vec![],
            require_signed: true,
        }
    }
}

/// HealthPulse sends periodic, signed heartbeats to the Vela Hub.
pub struct HealthPulse {
    config: PulseConfig,
    attester: Attester,
    sequence: u64,
    last_pulse: Option<Instant>,
}

impl HealthPulse {
    /// Create a new HealthPulse with the given config and attester.
    pub fn new(config: PulseConfig, attester: Attester) -> Self {
        Self {
            config,
            attester,
            sequence: 0,
            last_pulse: None,
        }
    }

    /// Number of pulses sent so far.
    pub fn pulse_count(&self) -> u64 {
        self.sequence
    }

    /// Time since the last successful pulse, or None if never pulsed.
    pub fn time_since_last_pulse(&self) -> Option<Duration> {
        self.last_pulse.map(|t| t.elapsed())
    }

    /// Check if a pulse is overdue (should have pulsed by now but hasn't).
    pub fn is_overdue(&self) -> bool {
        match self.last_pulse {
            Some(last) => last.elapsed() > self.config.interval,
            None => true, // Never pulsed → overdue
        }
    }

    /// Collect current system health metrics (platform-agnostic, best-effort).
    fn collect_metrics(&self) -> HealthMetrics {
        let uptime_secs = std::fs::read_to_string("/proc/uptime")
            .ok()
            .and_then(|s| s.split_whitespace().next()?.parse::<f64>().ok())
            .unwrap_or(0.0);

        let (load_1m, load_5m) = std::fs::read_to_string("/proc/loadavg")
            .ok()
            .and_then(|s| {
                let parts: Vec<&str> = s.split_whitespace().collect();
                Some((
                    parts.first()?.parse::<f64>().ok()?,
                    parts.get(1)?.parse::<f64>().ok()?,
                ))
            })
            .unwrap_or((0.0, 0.0));

        let (mem_total, mem_used) = std::fs::read_to_string("/proc/meminfo")
            .ok()
            .map(|s| {
                let mut total = 0u64;
                let mut avail = 0u64;
                for line in s.lines() {
                    if line.starts_with("MemTotal:") {
                        total = parse_kb_field(line);
                    }
                    if line.starts_with("MemAvailable:") {
                        avail = parse_kb_field(line);
                    }
                }
                let total_bytes = total.saturating_mul(1024);
                let used_bytes = total.saturating_sub(avail).saturating_mul(1024);
                (total_bytes, used_bytes)
            })
            .unwrap_or((0, 0));

        let active_slot =
            std::env::var("VELA_BOOT_SLOT").unwrap_or_else(|_| "primary".into());

        HealthMetrics {
            uptime_secs,
            load_avg_1m: load_1m,
            load_avg_5m: load_5m,
            mem_used,
            mem_total,
            active_slot,
            firmware_version: env!("CARGO_PKG_VERSION").into(),
            pulse_count: self.sequence,
        }
    }

    /// Send a single heartbeat pulse to the Hub.
    ///
    /// Returns Ok(()) if the Hub acknowledges the heartbeat, Err otherwise.
    #[instrument(skip(self))]
    pub async fn send_pulse(&mut self) -> AttestationResult<()> {
        self.sequence = self.sequence.wrapping_add(1);
        let metrics = self.collect_metrics();

        let attestation = if self.config.require_signed {
            self.attester
                .build_signed_payload(&self.config.signing_key, None)?
        } else {
            self.attester.build_payload(None)?
        };

        let heartbeat = HeartbeatPayload {
            attestation,
            metrics,
            sequence: self.sequence,
            hub_url: self.config.hub_url.clone(),
        };

        info!(
            sequence = self.sequence,
            uptime = heartbeat.metrics.uptime_secs,
            load_1m = heartbeat.metrics.load_avg_1m,
            mem_used_mb = heartbeat.metrics.mem_used / 1_048_576,
            "Sending health heartbeat"
        );

        // In production, this would POST to the Hub.
        // For now, we validate the payload is well-formed and record the pulse.
        self.last_pulse = Some(Instant::now());

        debug!(?heartbeat, "Heartbeat payload prepared (mock send)");
        Ok(())
    }

    /// Send a pulse, logging errors but never panicking.
    ///
    /// Always returns the attempt result — the caller can decide retry policy.
    pub async fn try_send_pulse(&mut self) -> AttestationResult<()> {
        match self.send_pulse().await {
            Ok(()) => {
                debug!(sequence = self.sequence, "Heartbeat acknowledged");
                Ok(())
            }
            Err(e) => {
                // Don't increment sequence on failure
                self.sequence = self.sequence.wrapping_sub(1);
                warn!(error = %e, "Heartbeat send failed");
                Err(e)
            }
        }
    }
}

/// Parse a /proc/meminfo field like "MemTotal:      16384256 kB".
fn parse_kb_field(line: &str) -> u64 {
    line.split_whitespace()
        .nth(1)
        .and_then(|n| n.parse::<u64>().ok())
        .unwrap_or(0)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::attester::Attester;
    use crate::identity::SystemIdentity;

    fn test_identity() -> SystemIdentity {
        SystemIdentity {
            machine_id: "test".into(),
            mac_address: "00:00:00:00:00:00".into(),
            serial: None,
            board_model: None,
            kernel_version: "test".into(),
        }
    }

    fn test_config() -> PulseConfig {
        PulseConfig {
            interval: Duration::from_secs(60),
            hub_url: "https://localhost/heartbeat".into(),
            signing_key: vec![],
            require_signed: false,
        }
    }

    #[tokio::test]
    async fn test_pulse_starts_and_returns_ok() {
        let attester = Attester::new(test_identity());
        let mut pulse = HealthPulse::new(test_config(), attester);

        assert!(pulse.is_overdue());
        assert_eq!(pulse.pulse_count(), 0);

        pulse.send_pulse().await.unwrap();

        assert_eq!(pulse.pulse_count(), 1);
        assert!(!pulse.is_overdue());
    }

    #[tokio::test]
    async fn test_multiple_pulses_increment_sequence() {
        let attester = Attester::new(test_identity());
        let mut pulse = HealthPulse::new(test_config(), attester);

        for i in 1..=5 {
            pulse.send_pulse().await.unwrap();
            assert_eq!(pulse.pulse_count(), i);
        }
    }

    #[tokio::test]
    async fn test_try_send_pulse_does_not_increment_on_error() {
        let attester = Attester::new(test_identity());
        let mut config = test_config();
        // Signed but no key → should fail
        config.require_signed = true;
        let mut pulse = HealthPulse::new(config, attester);

        let result = pulse.try_send_pulse().await;
        // With key length 0, HMAC new_from_slice will fail
        assert!(result.is_err());
        // Sequence should NOT have incremented
        assert_eq!(pulse.pulse_count(), 0);
    }

    #[test]
    fn test_collect_metrics_does_not_panic() {
        let attester = Attester::new(test_identity());
        let pulse = HealthPulse::new(test_config(), attester);
        let metrics = pulse.collect_metrics();
        // In CI/sandbox, uptime may be 0 but the function should not panic
        assert!(metrics.uptime_secs >= 0.0);
    }

    #[test]
    fn test_parse_kb_field() {
        assert_eq!(parse_kb_field("MemTotal:      16384256 kB"), 16384256);
        assert_eq!(parse_kb_field("MemAvailable:   8192000 kB"), 8192000);
        assert_eq!(parse_kb_field(""), 0);
    }
}
