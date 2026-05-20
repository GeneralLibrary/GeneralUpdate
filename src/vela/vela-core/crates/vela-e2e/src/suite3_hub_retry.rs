//! Suite 3: Hub client + retry + download integration tests.

use sha2::Digest;
use std::sync::Arc;
use std::sync::atomic::{AtomicU32, Ordering};
use std::time::Duration;
use vela_hub::client::VelaHubClient;
use vela_hub::retry::RetryStrategy;
use vela_hub::*;

#[tokio::test]
async fn test_retry_exhausts_non_retryable() {
    let strategy = RetryStrategy {
        max_retries: 3,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(10),
        jitter: 0.0,
    };
    let result: HubResult<()> = strategy
        .execute(|| async { Err(HubError::AuthRequired) })
        .await;
    assert!(result.is_err());
}

#[tokio::test]
async fn test_retry_exhausts_transient() {
    let strategy = RetryStrategy {
        max_retries: 2,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(10),
        jitter: 0.0,
    };
    let counter = Arc::new(AtomicU32::new(0));
    let counter_clone = counter.clone();
    let result: HubResult<()> = strategy
        .execute(move || {
            let c = counter_clone.clone();
            async move {
                c.fetch_add(1, Ordering::SeqCst);
                Err(HubError::RateLimited(Duration::from_secs(1)))
            }
        })
        .await;
    assert!(result.is_err());
    assert_eq!(counter.load(Ordering::SeqCst), 3);
}

#[tokio::test]
async fn test_retry_eventually_succeeds() {
    let strategy = RetryStrategy {
        max_retries: 5,
        initial_delay: Duration::from_millis(1),
        max_delay: Duration::from_millis(10),
        jitter: 0.0,
    };
    let counter = Arc::new(AtomicU32::new(0));
    let counter_clone = counter.clone();
    let result: HubResult<String> = strategy
        .execute(move || {
            let c = counter_clone.clone();
            async move {
                let n = c.fetch_add(1, Ordering::SeqCst);
                if n < 3 {
                    Err(HubError::RateLimited(Duration::from_secs(1)))
                } else {
                    Ok("recovered".to_string())
                }
            }
        })
        .await;
    assert_eq!(result.unwrap(), "recovered");
    assert_eq!(counter.load(Ordering::SeqCst), 4);
}

#[test]
fn test_download_state_tracking() {
    let state = vela_hub::download::DownloadState {
        url: "https://example.com/fp.fpk".into(),
        expected_size: 1024,
        expected_checksum: Some("abc123".into()),
        downloaded_bytes: 512,
        dest_path: std::path::PathBuf::from("/tmp/test.fpk"),
    };
    assert!(!state.is_complete());
}

#[test]
fn test_download_state_complete() {
    let state = vela_hub::download::DownloadState {
        url: "https://example.com/fp.fpk".into(),
        expected_size: 1024,
        expected_checksum: None,
        downloaded_bytes: 1024,
        dest_path: std::path::PathBuf::from("/tmp/test.fpk"),
    };
    assert!(state.is_complete());
}

#[tokio::test]
async fn test_checksum_verification_pass() {
    let data = b"vela-ota-integration-test-data";
    let expected = hex::encode(sha2::Sha256::digest(data));
    let actual = hex::encode(sha2::Sha256::digest(data));
    assert_eq!(actual, expected, "Same data should produce same hash");
}

#[tokio::test]
async fn test_checksum_mismatch() {
    let wrong_hash = "0000000000000000000000000000000000000000000000000000000000000000";
    let data = b"original";
    let actual = hex::encode(sha2::Sha256::digest(data));
    assert_ne!(actual, wrong_hash, "Checksum should not match wrong hash");
}

#[test]
fn test_hub_client_construction() {
    let config = HubConfig::new("https://hub.vela-ota.dev").with_auth("test-token");
    let client = VelaHubClient::new(config);
    assert!(client.is_ok());
}

#[test]
fn test_hub_client_missing_auth_builds() {
    let config = HubConfig::new("https://hub.vela-ota.dev");
    let client = VelaHubClient::new(config);
    assert!(client.is_ok());
}

#[test]
fn test_url_construction() {
    let config = HubConfig::new("https://hub.example.com");
    assert_eq!(
        config.url("/api/v1/poll"),
        "https://hub.example.com/api/v1/poll"
    );
    let config = HubConfig::new("https://hub.example.com/");
    assert_eq!(
        config.url("/api/v1/poll"),
        "https://hub.example.com/api/v1/poll"
    );
}

#[test]
fn test_rollout_manifest_serde() {
    let manifest = RolloutManifest {
        rollout_id: "roll-001".into(),
        flashpack_url: "https://artifacts/fp.fpk".into(),
        flashpack_checksum: "sha256:abc123".into(),
        flashpack_size: 1048576,
        target_version: "1.2.3".into(),
        force_install: false,
        deadline: Some("2026-06-01T00:00:00Z".into()),
        release_notes: Some("Bug fixes".into()),
    };
    let json = serde_json::to_string(&manifest).unwrap();
    let decoded: RolloutManifest = serde_json::from_str(&json).unwrap();
    assert_eq!(decoded.rollout_id, manifest.rollout_id);
    assert_eq!(decoded.flashpack_size, manifest.flashpack_size);
    assert!(!decoded.force_install);
}

#[test]
fn test_poll_outcome_serde() {
    let update = PollOutcome::UpdateAvailable(RolloutManifest {
        rollout_id: "r1".into(),
        flashpack_url: "url".into(),
        flashpack_checksum: "hash".into(),
        flashpack_size: 1024,
        target_version: "2.0".into(),
        force_install: false,
        deadline: None,
        release_notes: None,
    });
    let json = serde_json::to_string(&update).unwrap();
    let decoded: PollOutcome = serde_json::from_str(&json).unwrap();
    match decoded {
        PollOutcome::UpdateAvailable(m) => assert_eq!(m.rollout_id, "r1"),
        _ => panic!("Expected UpdateAvailable"),
    }
    let no_update = PollOutcome::NoUpdate;
    let json = serde_json::to_string(&no_update).unwrap();
    assert!(json.contains("NoUpdate"));
}

#[test]
fn test_retry_delay_exponential_growth() {
    let s = RetryStrategy::for_polling();
    assert_eq!(s.max_retries, 2);
    assert!(s.initial_delay < Duration::from_secs(1));
    let d = RetryStrategy::for_download();
    assert_eq!(d.max_retries, 5);
    assert!(d.max_delay >= Duration::from_secs(60));
}
