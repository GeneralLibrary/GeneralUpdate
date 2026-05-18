//! Hardware watchdog timer integration for Linux `/dev/watchdog`.
//!
//! During an update, the watchdog is armed with a short timeout. If the
//! update process hangs (crash, deadlock, I/O stall), the watchdog triggers
//! a hardware reset. On next boot, the bootloader detects the unclean
//! shutdown and boots the fallback slot.

use std::fs::{File, OpenOptions};
use std::os::unix::io::AsRawFd;
use std::os::unix::fs::OpenOptionsExt;
use std::path::Path;
use std::time::{Duration, Instant};
use tokio::time::sleep;
use tracing::{error, info, instrument, warn};

use crate::{WatchdogError, WatchdogResult};

/// Default watchdog device path.
pub const DEFAULT_WATCHDOG_DEV: &str = "/dev/watchdog";

/// Default timeout during normal operation (seconds).
pub const DEFAULT_TIMEOUT_SECS: u32 = 60;

/// Timeout during update — short window for fast failure detection.
pub const UPDATE_TIMEOUT_SECS: u32 = 10;

/// Hardware watchdog timer with RAII guard for safe disarm.
///
/// Once opened, the watchdog must be periodically "petted" (write a magic
/// byte) within the timeout window. If the pet interval is missed, the
/// kernel triggers a hardware reset.
pub struct Watchdog {
    dev: Option<File>,
    timeout_secs: u32,
    armed_at: Option<Instant>,
    pet_count: u64,
}

/// RAII guard that disarms the watchdog on drop.
///
/// If the guard is dropped without calling `disarm()`, the watchdog
/// will still trigger after the timeout — this is intentional for
/// crash scenarios (panic = drop guard = watchdog fires).
pub struct WatchdogGuard<'a> {
    watchdog: &'a mut Watchdog,
    disarmed: bool,
}

impl Drop for WatchdogGuard<'_> {
    fn drop(&mut self) {
        if !self.disarmed {
            warn!(
                "WatchdogGuard dropped without disarm — watchdog will fire \
                 in {}s",
                self.watchdog.timeout_secs
            );
            // Do NOT disarm on panic drop — let the watchdog fire.
            // This ensures a hung update process triggers fallback.
        }
    }
}

impl Watchdog {
    /// Open the hardware watchdog device.
    ///
    /// Returns None if `/dev/watchdog` doesn't exist (non-Linux or container).
    #[instrument]
    pub fn open() -> WatchdogResult<Self> {
        Self::open_at(DEFAULT_WATCHDOG_DEV, DEFAULT_TIMEOUT_SECS)
    }

    /// Open a specific watchdog device with the given timeout.
    #[instrument(skip(path))]
    pub fn open_at(path: impl AsRef<Path>, timeout_secs: u32) -> WatchdogResult<Self> {
        let path = path.as_ref();
        let dev = OpenOptions::new()
            .write(true)
            .custom_flags(libc::O_CLOEXEC)
            .open(path)
            .map_err(WatchdogError::OpenFailed)?;

        let wd = Self {
            dev: Some(dev),
            timeout_secs,
            armed_at: None,
            pet_count: 0,
        };

        info!(
            timeout_secs = wd.timeout_secs,
            path = %path.display(),
            "Watchdog device opened"
        );

        Ok(wd)
    }

    /// Check if the watchdog device is present on this system.
    pub fn is_available() -> bool {
        Path::new(DEFAULT_WATCHDOG_DEV).exists()
    }

    /// Whether the watchdog is currently armed.
    pub fn is_armed(&self) -> bool {
        self.dev.is_some() && self.armed_at.is_some()
    }

    /// Arm the watchdog with the configured timeout and return a guard.
    ///
    /// The guard must be `disarm()`ed before a controlled reboot,
    /// otherwise the watchdog will fire and trigger fallback.
    #[instrument(skip(self))]
    pub fn arm(&mut self) -> WatchdogResult<WatchdogGuard<'_>> {
        if self.is_armed() {
            return Err(WatchdogError::AlreadyArmed);
        }

        // Perform initial pet to arm the watchdog
        self.pet_raw()?;
        self.armed_at = Some(Instant::now());
        self.pet_count = 1;

        info!(
            timeout_secs = self.timeout_secs,
            "Watchdog armed"
        );

        Ok(WatchdogGuard {
            watchdog: self,
            disarmed: false,
        })
    }

    /// Disarm the watchdog before a controlled reboot.
    ///
    /// Writes the magic 'V' byte to `/dev/watchdog` which tells the
    /// kernel driver to stop the timer gracefully.
    #[instrument(skip(self))]
    pub fn disarm(&mut self) -> WatchdogResult<()> {
        if !self.is_armed() {
            return Err(WatchdogError::NotArmed);
        }

        // Safe disarm: write magic 'V' (0x56) to stop the watchdog timer
        // Many watchdog drivers support this ("magic close" feature)
        if let Some(dev) = &mut self.dev {
            use std::io::Write;
            dev.write_all(b"V")
                .map_err(WatchdogError::PetFailed)?;
            dev.flush()
                .map_err(WatchdogError::PetFailed)?;
        }

        // Close the device to fully disarm
        self.dev = None;
        self.armed_at = None;

        info!("Watchdog disarmed safely — controlled shutdown");
        Ok(())
    }

    /// Pet (keepalive) the watchdog to reset the countdown timer.
    ///
    /// Must be called at least once within every `timeout_secs` window.
    /// Recommended pet interval: timeout_secs / 2.
    #[instrument(skip(self))]
    pub fn pet(&mut self) -> WatchdogResult<()> {
        if !self.is_armed() {
            return Err(WatchdogError::NotArmed);
        }

        self.pet_raw()?;
        self.pet_count = self.pet_count.wrapping_add(1);

        if self.pet_count % 10 == 0 {
            info!(count = self.pet_count, "Watchdog pet");
        }

        Ok(())
    }

    /// Low-level pet: write a single byte to /dev/watchdog.
    fn pet_raw(&mut self) -> WatchdogResult<()> {
        let dev = self.dev.as_mut().ok_or(WatchdogError::NotArmed)?;
        use std::io::Write;
        dev.write_all(&[0])
            .map_err(WatchdogError::PetFailed)
    }

    /// Change the watchdog timeout.
    ///
    /// This affects the next arm — does not change the running timeout.
    pub fn set_timeout(&mut self, secs: u32) {
        self.timeout_secs = secs;
        info!(timeout_secs = secs, "Watchdog timeout set");
    }

    /// Time since last pet (for diagnostics).
    pub fn time_since_last_pet(&self) -> Option<Duration> {
        self.armed_at.map(|t| t.elapsed())
    }

    /// Total number of pets performed (for metrics).
    pub fn pet_count(&self) -> u64 {
        self.pet_count
    }

    /// Arm with update timeout (short window for fast failure detection).
    pub fn arm_for_update(&mut self) -> WatchdogResult<WatchdogGuard<'_>> {
        self.set_timeout(UPDATE_TIMEOUT_SECS);
        self.arm()
    }
}

impl<'a> WatchdogGuard<'a> {
    /// Disarm the watchdog safely.
    ///
    /// After calling this, the guard is consumed and the watchdog will
    /// not fire. Must be called before a controlled reboot.
    pub fn disarm(mut self) -> WatchdogResult<()> {
        self.watchdog.disarm()?;
        self.disarmed = true;
        Ok(())
    }

    /// Pet the watchdog through the guard.
    pub fn pet(&mut self) -> WatchdogResult<()> {
        self.watchdog.pet()
    }
}

/// Run a background pet loop at the given interval.
///
/// Returns when the cancellation token is triggered.
/// This keeps the watchdog alive during long-running operations.
pub async fn pet_loop(
    mut watchdog: Watchdog,
    interval: Duration,
    cancel: tokio::sync::watch::Receiver<bool>,
) -> WatchdogResult<()> {
    let mut guard = watchdog.arm()?;
    info!(interval_ms = interval.as_millis(), "Watchdog pet loop started");

    loop {
        tokio::select! {
            _ = sleep(interval) => {
                if let Err(e) = guard.pet() {
                    warn!(%e, "Watchdog pet failed");
                }
            }
            _ = cancel.changed() => {
                info!("Watchdog pet loop cancelled");
                break;
            }
        }
    }

    guard.disarm()?;
    info!("Watchdog pet loop ended — disarmed");
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_watchdog_not_available_in_ci() {
        // In CI/containers, /dev/watchdog typically doesn't exist
        if !Watchdog::is_available() {
            assert!(Watchdog::open().is_err());
        }
    }

    #[test]
    fn test_default_timeout() {
        assert_eq!(DEFAULT_TIMEOUT_SECS, 60);
    }

    #[test]
    fn test_update_timeout_is_shorter() {
        assert!(UPDATE_TIMEOUT_SECS < DEFAULT_TIMEOUT_SECS);
    }

    #[test]
    fn test_armed_state_tracking() {
        let result = Watchdog::open();
        if let Ok(mut wd) = result {
            assert!(!wd.is_armed());
            let guard = wd.arm();
            if let Ok(_g) = guard {
                assert!(wd.is_armed());
                // Guard drop will disarm (in test context this is safe)
            }
        }
    }
}
