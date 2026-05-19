//! Authenticated HTTP client for the Vela Hub.

use reqwest::header::{AUTHORIZATION, CONTENT_TYPE, HeaderMap, HeaderValue, RANGE};
use std::time::Duration;
use tracing::{debug, error, info, instrument, warn};

use crate::retry::RetryStrategy;
use crate::{HubConfig, HubError, HubResult, PollOutcome, RolloutManifest};

// ─── client builder ──────────────────────────────────────────────

/// Typed HTTP client for Vela Hub communication.
///
/// Uses reqwest with connection pooling, auth headers,
/// and structured tracing on all calls.
pub struct VelaHubClient {
    inner: reqwest::Client,
    config: HubConfig,
}

impl VelaHubClient {
    /// Build a new client from configuration.
    pub fn new(config: HubConfig) -> HubResult<Self> {
        let mut builder = reqwest::Client::builder()
            .timeout(Duration::from_secs(30))
            .connect_timeout(Duration::from_secs(10))
            .pool_idle_timeout(Duration::from_secs(90))
            .pool_max_idle_per_host(5)
            .user_agent(format!("vela-ota/{}", env!("CARGO_PKG_VERSION")));

        // mTLS if configured
        if let (Some(cert_path), Some(key_path)) = (&config.tls_client_cert, &config.tls_client_key)
        {
            let cert = std::fs::read(cert_path)
                .map_err(|e| HubError::InvalidUrl(format!("TLS cert: {e}")))?;
            let key = std::fs::read(key_path)
                .map_err(|e| HubError::InvalidUrl(format!("TLS key: {e}")))?;
            let identity = reqwest::Identity::from_pem(&[cert, key].concat())
                .map_err(|e| HubError::InvalidUrl(format!("Identity PEM: {e}")))?;
            builder = builder.identity(identity);
        }

        if let Some(ca_path) = &config.tls_ca_cert {
            let ca = std::fs::read(ca_path)
                .map_err(|e| HubError::InvalidUrl(format!("CA cert: {e}")))?;
            let cert = reqwest::Certificate::from_pem(&ca)
                .map_err(|e| HubError::InvalidUrl(format!("CA PEM: {e}")))?;
            builder = builder.add_root_certificate(cert);
        }

        Ok(Self {
            inner: builder.build()?,
            config,
        })
    }

    /// Return the auth token, or the appropriate error.
    fn auth_token(&self) -> HubResult<&str> {
        self.config
            .auth_token
            .as_deref()
            .ok_or(HubError::AuthRequired)
    }

    /// Build base headers including auth.
    fn headers(&self, extra: Option<HeaderMap>) -> HubResult<HeaderMap> {
        let mut headers = HeaderMap::new();
        headers.insert(
            AUTHORIZATION,
            HeaderValue::from_str(&format!("Bearer {}", self.auth_token()?))
                .map_err(|e| HubError::InvalidUrl(format!("Bad token: {e}")))?,
        );
        headers.insert(CONTENT_TYPE, HeaderValue::from_static("application/json"));
        headers.insert("Accept", HeaderValue::from_static("application/json"));

        if let Some(extra) = extra {
            headers.extend(extra);
        }

        Ok(headers)
    }

    /// Map an HTTP response to a HubResult, handling error statuses.
    async fn handle_response(resp: reqwest::Response) -> HubResult<reqwest::Response> {
        debug!(
            status = %resp.status(),
            url = %resp.url(),
            "Hub HTTP response"
        );

        match resp.error_for_status_ref() {
            Ok(_) => Ok(resp),
            Err(e) => {
                let status = resp.status();
                let body = resp.text().await.unwrap_or_default();

                match status.as_u16() {
                    401 => Err(HubError::AuthRequired),
                    429 => {
                        let retry_after = std::time::Duration::from_secs(60);
                        Err(HubError::RateLimited(retry_after))
                    }
                    code => {
                        warn!(status = code, %body, "Hub returned error status");
                        Err(HubError::HttpStatus(code, body))
                    }
                }
            }
        }
    }

    // ─── endpoints ──────────────────────────────────────────────

    /// Poll the Hub for available updates.
    ///
    /// GET /api/v1/rollout/poll
    #[instrument(skip(self))]
    pub async fn poll_for_update(
        &self,
        current_version: &str,
        device_id: &str,
    ) -> HubResult<PollOutcome> {
        let url = self.config.url("/api/v1/rollout/poll");
        let headers = self.headers(None)?;

        debug!(%current_version, %device_id, "Polling Hub for updates");

        let resp = self
            .inner
            .get(&url)
            .headers(headers)
            .query(&[
                ("current_version", current_version),
                ("device_id", device_id),
            ])
            .send()
            .await?;

        let resp = Self::handle_response(resp).await?;
        let outcome: PollOutcome = resp.json().await?;
        info!(?outcome, "Hub poll result");

        match &outcome {
            PollOutcome::UpdateAvailable(manifest) => {
                info!(
                    rollout_id = %manifest.rollout_id,
                    target_version = %manifest.target_version,
                    "Update available from Hub"
                );
            }
            PollOutcome::NoUpdate => {
                debug!("No update available");
            }
            PollOutcome::RetryLater { retry_after_secs } => {
                info!(%retry_after_secs, "Hub requested retry later");
            }
        }

        Ok(outcome)
    }

    /// Poll with retry for transient failures.
    #[instrument(skip(self, retry))]
    pub async fn poll_with_retry(
        &self,
        current_version: &str,
        device_id: &str,
        retry: &RetryStrategy,
    ) -> HubResult<PollOutcome> {
        retry
            .execute(|| self.poll_for_update(current_version, device_id))
            .await
    }

    /// Submit a signed attestation payload to the Hub.
    ///
    /// POST /api/v1/attest
    #[instrument(skip(self, attestation))]
    pub async fn submit_attestation<T: serde::Serialize>(&self, attestation: &T) -> HubResult<()> {
        let url = self.config.url("/api/v1/attest");
        let headers = self.headers(None)?;

        debug!("Submitting device attestation to Hub");

        let resp = self
            .inner
            .post(&url)
            .headers(headers)
            .json(attestation)
            .send()
            .await?;

        Self::handle_response(resp).await?;
        info!("Attestation submitted successfully");
        Ok(())
    }

    /// Send a health heartbeat to the Hub.
    ///
    /// POST /api/v1/heartbeat
    #[instrument(skip(self, heartbeat))]
    pub async fn send_heartbeat<T: serde::Serialize>(&self, heartbeat: &T) -> HubResult<()> {
        let url = self.config.url("/api/v1/heartbeat");
        let headers = self.headers(None)?;

        debug!("Sending health heartbeat");

        let resp = self
            .inner
            .post(&url)
            .headers(headers)
            .json(heartbeat)
            .send()
            .await?;

        Self::handle_response(resp).await?;
        info!("Heartbeat acknowledged by Hub");
        Ok(())
    }

    /// Check Hub connectivity with a simple health check.
    ///
    /// GET /api/v1/health
    #[instrument(skip(self))]
    pub async fn health_check(&self) -> HubResult<bool> {
        let url = self.config.url("/api/v1/health");
        match self.inner.get(&url).send().await {
            Ok(resp) => {
                let ok = resp.status().is_success();
                info!(ok, "Hub health check");
                Ok(ok)
            }
            Err(e) => {
                warn!(%e, "Hub health check failed");
                Err(HubError::Http(e))
            }
        }
    }
}

// ─── tests ──────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_client_builds_with_minimal_config() {
        let config = HubConfig::new("https://localhost:8443");
        let client = VelaHubClient::new(config);
        assert!(client.is_ok());
    }

    #[test]
    fn test_client_builds_with_auth() {
        let config = HubConfig::new("https://localhost:8443").with_auth("test-token-abc");
        let client = VelaHubClient::new(config).unwrap();
        assert_eq!(client.auth_token().unwrap(), "test-token-abc");
    }

    #[test]
    fn test_client_missing_auth_returns_error() {
        let config = HubConfig::new("https://localhost:8443");
        let client = VelaHubClient::new(config).unwrap();
        assert!(matches!(
            client.auth_token().unwrap_err(),
            HubError::AuthRequired
        ));
    }

    #[test]
    fn test_url_construction() {
        let config = HubConfig::new("https://hub.example.com");
        assert_eq!(
            config.url("/api/v1/rollout/poll"),
            "https://hub.example.com/api/v1/rollout/poll"
        );
    }

    #[test]
    fn test_url_construction_trailing_slash() {
        let config = HubConfig::new("https://hub.example.com/");
        assert_eq!(
            config.url("/api/v1/rollout/poll"),
            "https://hub.example.com/api/v1/rollout/poll"
        );
    }

    #[test]
    fn test_rollout_manifest_serde() {
        let json = r#"{
            "rollout_id": "roll-001",
            "flashpack_url": "https://artifacts.vela.example/fp-1.2.3.tar.gz",
            "flashpack_checksum": "sha256:abc123",
            "flashpack_size": 1048576,
            "target_version": "1.2.3",
            "force_install": false,
            "deadline": null,
            "release_notes": "Bug fixes and performance improvements"
        }"#;
        let manifest: RolloutManifest = serde_json::from_str(json).unwrap();
        assert_eq!(manifest.rollout_id, "roll-001");
        assert_eq!(manifest.flashpack_size, 1_048_576);
        assert!(!manifest.force_install);
    }
}
