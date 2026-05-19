//! Exponential backoff retry strategy for Hub HTTP operations.

use std::future::Future;
use std::time::Duration;
use tokio::time::sleep;
use tracing::{debug, warn};

use crate::{HubError, HubResult};

/// Exponential backoff retry with jitter.
///
/// For transient Hub failures (5xx, network errors, rate limits),
/// retries with increasing delay: initial → 2× → 4× → 8× ... up to max.
#[derive(Debug, Clone)]
pub struct RetryStrategy {
    /// Maximum number of retry attempts (excluding the initial try).
    pub max_retries: u32,
    /// Initial delay before first retry.
    pub initial_delay: Duration,
    /// Maximum delay cap.
    pub max_delay: Duration,
    /// Jitter factor (0.0–1.0) applied to delay for staggering.
    pub jitter: f64,
}

impl Default for RetryStrategy {
    fn default() -> Self {
        Self {
            max_retries: 3,
            initial_delay: Duration::from_secs(1),
            max_delay: Duration::from_secs(30),
            jitter: 0.2,
        }
    }
}

impl RetryStrategy {
    /// Create a strategy suitable for polling operations (aggressive retry).
    pub fn for_polling() -> Self {
        Self {
            max_retries: 2,
            initial_delay: Duration::from_millis(500),
            max_delay: Duration::from_secs(5),
            jitter: 0.3,
        }
    }

    /// Create a strategy suitable for download operations (gentle retry).
    pub fn for_download() -> Self {
        Self {
            max_retries: 5,
            initial_delay: Duration::from_secs(5),
            max_delay: Duration::from_secs(120),
            jitter: 0.25,
        }
    }

    /// Compute the delay for a given attempt number (0-indexed).
    fn delay_for_attempt(&self, attempt: u32) -> Duration {
        let base = self
            .initial_delay
            .checked_mul(2u32.saturating_pow(attempt))
            .unwrap_or(self.max_delay);

        let capped = base.min(self.max_delay);

        if self.jitter > 0.0 {
            let jitter_ms = (capped.as_millis() as f64 * self.jitter) as u64;
            let jitter_offset = if jitter_ms > 0 {
                // Simple deterministic jitter using attempt as seed
                (attempt as u64 * 7919) % jitter_ms
            } else {
                0
            };
            capped + Duration::from_millis(jitter_offset)
        } else {
            capped
        }
    }

    /// Determine if the error is retryable (transient).
    fn is_retryable(err: &HubError) -> bool {
        matches!(
            err,
            HubError::Http(_) | HubError::RateLimited(_) | HubError::DownloadInterrupted(_, _)
        )
    }

    /// Execute an async operation with retry.
    ///
    /// The closure `f` is called at most `max_retries + 1` times
    /// (initial call + up to `max_retries` retries).
    pub async fn execute<F, Fut, T>(&self, f: F) -> HubResult<T>
    where
        F: Fn() -> Fut,
        Fut: Future<Output = HubResult<T>>,
    {
        let mut last_err = None;

        for attempt in 0..=self.max_retries {
            if attempt > 0 {
                let delay = self.delay_for_attempt(attempt - 1);
                debug!(
                    attempt,
                    delay_ms = delay.as_millis(),
                    "Retrying Hub request"
                );
                sleep(delay).await;
            }

            match f().await {
                Ok(value) => return Ok(value),
                Err(err) => {
                    let retryable = Self::is_retryable(&err);

                    if !retryable || attempt == self.max_retries {
                        warn!(
                            attempt = attempt + 1,
                            retryable,
                            %err,
                            "Hub request failed permanently"
                        );
                        return Err(err);
                    }

                    warn!(
                        attempt = attempt + 1,
                        %err,
                        "Hub request failed, will retry"
                    );
                    last_err = Some(err);
                }
            }
        }

        Err(last_err.unwrap_or(HubError::NotConfigured))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Arc;
    use std::sync::atomic::{AtomicU32, Ordering};

    #[test]
    fn test_delay_grows_exponentially() {
        let strategy = RetryStrategy::default();
        let d0 = strategy.delay_for_attempt(0);
        let d1 = strategy.delay_for_attempt(1);
        let d2 = strategy.delay_for_attempt(2);
        assert!(d1 > d0);
        assert!(d2 > d1);
    }

    #[test]
    fn test_delay_capped_at_max() {
        let strategy = RetryStrategy {
            max_delay: Duration::from_secs(5),
            ..Default::default()
        };
        let delay = strategy.delay_for_attempt(10);
        assert!(delay <= strategy.max_delay + Duration::from_millis(500));
    }

    #[tokio::test]
    async fn test_retry_succeeds_on_first_try() {
        let strategy = RetryStrategy::default();
        let result = strategy.execute(|| async { Ok("success") }).await;
        assert_eq!(result.unwrap(), "success");
    }

    #[tokio::test]
    async fn test_retry_eventually_fails_non_retryable() {
        let strategy = RetryStrategy {
            max_retries: 2,
            initial_delay: Duration::from_millis(1),
            ..Default::default()
        };
        // AuthRequired is not retryable
        let result: HubResult<()> = strategy
            .execute(|| async { Err(HubError::AuthRequired) })
            .await;
        assert!(result.is_err());
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

        let result: HubResult<&str> = strategy
            .execute(move || {
                let cnt = counter_clone.clone();
                async move {
                    let n = cnt.fetch_add(1, Ordering::SeqCst);
                    if n < 3 {
                        Err(HubError::Http(reqwest::Error::from(std::io::Error::new(
                            std::io::ErrorKind::ConnectionReset,
                            "mock",
                        ))))
                    } else {
                        Ok("eventually")
                    }
                }
            })
            .await;

        assert_eq!(result.unwrap(), "eventually");
        assert_eq!(counter.load(Ordering::SeqCst), 4); // 3 fails + 1 success
    }

    #[test]
    fn test_is_retryable() {
        assert!(RetryStrategy::is_retryable(&HubError::Http(
            reqwest::Error::from(std::io::Error::new(std::io::ErrorKind::TimedOut, "timeout",))
        )));
        assert!(RetryStrategy::is_retryable(&HubError::RateLimited(
            Duration::from_secs(60)
        )));
        assert!(RetryStrategy::is_retryable(&HubError::DownloadInterrupted(
            100, 200
        )));
        assert!(!RetryStrategy::is_retryable(&HubError::AuthRequired));
        assert!(!RetryStrategy::is_retryable(&HubError::NotConfigured));
    }

    #[test]
    fn test_polling_strategy_config() {
        let s = RetryStrategy::for_polling();
        assert_eq!(s.max_retries, 2);
        assert!(s.initial_delay < Duration::from_secs(1));
    }

    #[test]
    fn test_download_strategy_config() {
        let s = RetryStrategy::for_download();
        assert_eq!(s.max_retries, 5);
        assert!(s.max_delay >= Duration::from_secs(60));
    }
}
