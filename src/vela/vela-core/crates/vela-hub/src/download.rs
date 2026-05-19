//! FlashPack artifact download with range-based resume support.

use reqwest::header::{HeaderMap, HeaderValue, RANGE};
use std::path::PathBuf;
use tokio::fs;
use tokio::io::AsyncWriteExt;
use tracing::{debug, info, instrument, warn};

use crate::retry::RetryStrategy;
use crate::{HubConfig, HubError, HubResult};

/// Download progress callback.
pub type ProgressFn = Box<dyn Fn(u64, u64) + Send + Sync>;

/// State for a resumable artifact download.
#[derive(Debug, Clone)]
pub struct DownloadState {
    pub url: String,
    pub expected_size: u64,
    pub expected_checksum: Option<String>,
    pub downloaded_bytes: u64,
    pub dest_path: PathBuf,
}

impl DownloadState {
    pub fn is_complete(&self) -> bool {
        self.expected_size > 0 && self.downloaded_bytes >= self.expected_size
    }
}

/// Download a FlashPack artifact with resume support.
#[instrument(skip(config, dest_path))]
pub async fn download_artifact(
    config: &HubConfig,
    url: &str,
    expected_size: u64,
    expected_checksum: Option<&str>,
    dest_path: PathBuf,
) -> HubResult<PathBuf> {
    let mut state = load_existing_state(&dest_path).await;

    if let Some(ref s) = state {
        info!(
            downloaded = s.downloaded_bytes,
            total = expected_size,
            "Resuming partial download"
        );
    } else {
        state = Some(DownloadState {
            url: url.to_string(),
            expected_size,
            expected_checksum: expected_checksum.map(String::from),
            downloaded_bytes: 0,
            dest_path: dest_path.clone(),
        });
    }

    let state = state.unwrap();

    if state.downloaded_bytes >= expected_size && expected_size > 0 {
        info!(path = %dest_path.display(), "Artifact already fully downloaded");
        verify_checksum(&dest_path, expected_checksum).await?;
        return Ok(dest_path);
    }

    let client = reqwest::Client::new();
    let mut headers = HeaderMap::new();
    if let Some(token) = &config.auth_token {
        headers.insert(
            reqwest::header::AUTHORIZATION,
            HeaderValue::from_str(&format!("Bearer {token}"))
                .map_err(|e| HubError::InvalidUrl(format!("Bad token: {e}")))?,
        );
    }

    if state.downloaded_bytes > 0 {
        let range = format!("bytes={}-", state.downloaded_bytes);
        headers.insert(RANGE, HeaderValue::from_str(&range).map_err(|e| {
            HubError::InvalidUrl(format!("Bad range header: {e}"))
        })?);
        debug!(%range, "Sending range request for resume");
    }

    let resp = client
        .get(url)
        .headers(headers)
        .send()
        .await
        .map_err(HubError::Http)?;

    if !resp.status().is_success() && resp.status() != reqwest::StatusCode::PARTIAL_CONTENT {
        let status = resp.status().as_u16();
        let body = resp.text().await.unwrap_or_default();
        return Err(HubError::HttpStatus(status, body));
    }

    let mut file = if state.downloaded_bytes > 0 {
        tokio::fs::OpenOptions::new()
            .append(true)
            .open(&dest_path)
            .await
            .map_err(|e| HubError::InvalidUrl(format!("Cannot open for append: {e}")))?
    } else {
        tokio::fs::File::create(&dest_path)
            .await
            .map_err(|e| HubError::InvalidUrl(format!("Cannot create file: {e}")))?
    };

    let mut downloaded = state.downloaded_bytes;

    let body = resp.bytes().await.map_err(HubError::Http)?;
    file.write_all(&body)
        .await
        .map_err(|e| HubError::InvalidUrl(format!("Write error: {e}")))?;
    downloaded += body.len() as u64;

    file.flush()
        .await
        .map_err(|e| HubError::InvalidUrl(format!("Flush error: {e}")))?;

    if expected_size > 0 && downloaded < expected_size {
        warn!(downloaded, expected = expected_size, "Download incomplete");
        return Err(HubError::DownloadInterrupted(downloaded, expected_size));
    }

    info!(downloaded, path = %dest_path.display(), "Artifact download complete");

    verify_checksum(&dest_path, expected_checksum).await?;

    Ok(dest_path)
}

/// Download with retry for transient network failures.
#[instrument(skip(config, dest_path, retry))]
pub async fn download_with_retry(
    config: &HubConfig,
    url: &str,
    expected_size: u64,
    expected_checksum: Option<&str>,
    dest_path: PathBuf,
    retry: &RetryStrategy,
) -> HubResult<PathBuf> {
    let url_owned = url.to_string();
    let dest_clone = dest_path.clone();

    retry
        .execute(|| {
            download_artifact(
                config,
                &url_owned,
                expected_size,
                expected_checksum,
                dest_clone.clone(),
            )
        })
        .await
}

async fn load_existing_state(path: &std::path::Path) -> Option<DownloadState> {
    if !path.exists() {
        return None;
    }
    let metadata = fs::metadata(path).await.ok()?;
    let size = metadata.len();
    if size == 0 {
        return None;
    }
    if let Some(parent) = path.parent() {
        if !parent.exists() {
            return None;
        }
    }
    None
}

async fn verify_checksum(
    path: &std::path::Path,
    expected_hex: Option<&str>,
) -> HubResult<()> {
    let Some(expected) = expected_hex else {
        debug!("No checksum provided — skipping verification");
        return Ok(());
    };

    let data = fs::read(path)
        .await
        .map_err(|e| HubError::InvalidUrl(format!("Cannot read for checksum: {e}")))?;

    use sha2::Digest;
    let mut hasher = sha2::Sha256::new();
    hasher.update(&data);
    let actual_hex = hex::encode(hasher.finalize());

    if actual_hex.eq_ignore_ascii_case(expected) {
        info!(%actual_hex, "Checksum verified");
        Ok(())
    } else {
        warn!(expected = %expected, actual = %actual_hex, "Checksum mismatch");
        let _ = fs::remove_file(path).await;
        Err(HubError::ChecksumMismatch {
            expected: expected.to_string(),
            actual: actual_hex,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_download_state_is_complete() {
        let state = DownloadState {
            url: "https://example.com/artifact.tar.gz".into(),
            expected_size: 1024,
            expected_checksum: None,
            downloaded_bytes: 1024,
            dest_path: PathBuf::from("/tmp/test.tar.gz"),
        };
        assert!(state.is_complete());
    }

    #[test]
    fn test_download_state_not_complete() {
        let state = DownloadState {
            url: "https://example.com/artifact.tar.gz".into(),
            expected_size: 1024,
            expected_checksum: None,
            downloaded_bytes: 512,
            dest_path: PathBuf::from("/tmp/test.tar.gz"),
        };
        assert!(!state.is_complete());
    }

    #[test]
    fn test_download_state_size_zero() {
        let state = DownloadState {
            url: "https://example.com/artifact.tar.gz".into(),
            expected_size: 0,
            expected_checksum: None,
            downloaded_bytes: 0,
            dest_path: PathBuf::from("/tmp/test.tar.gz"),
        };
        assert!(!state.is_complete());
    }

    #[tokio::test]
    async fn test_checksum_skip_when_none() {
        let result = verify_checksum(std::path::Path::new("/nonexistent"), None).await;
        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn test_checksum_verification_missing_file() {
        let result = verify_checksum(std::path::Path::new("/nonexistent"), Some("abc")).await;
        assert!(result.is_err());
    }
}
