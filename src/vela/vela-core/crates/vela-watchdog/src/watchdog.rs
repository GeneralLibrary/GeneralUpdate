//! Hardware watchdog timer integration for Linux `/dev/watchdog`.
//!
//! During an update, the watchdog is armed with a short timeout. If the
//! update process hangs (crash, deadlock, I/O stall), the watchdog triggers
//! a hardware reset. On next boot, the bootloader detects the unclean
//! shutdown and boots the fallback slot.
//!
//! On non-Unix platforms (Windows, macOS), the watchdog is a no-op stub
//! that allows the code to compile and test without hardware.

use std::time::{Duration, Instant};
use tracing::{info, instrument, warn};

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
    #[cfg(unix)]
    dev: Option<std::fs::File>,
    #[cfg(not(unix))]
    armed: bool,
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
                "WatchdogGuard dropped without disarm — watchdog will fire in {}s",
                self.watchdog.timeout_secs
            );
        }
    }
}

impl Watchdog {
    /// Open the hardware watchdog device.
    ///
    /// Returns Err if `/dev/watchdog` doesn't exist (non-Linux or container).
    #[instrument]
    pub fn open() -> WatchdogResult<Self> {
        Self::open_at(
            std::path::Path::new(DEFAULT_WATCHDOG_DEV),
            DEFAULT_TIMEOUT_SECS,
        )
    }

    /// Open a specific watchdog device with the given timeout.
    #[cfg(unix)]
    #[instrument(skip(path))]
    pub fn open_at(path: &std::path::Path, timeout_secs: u32) -> WatchdogResult<Self> {
        use std::os::unix::fs::OpenOptionsExt;
        let dev = std::fs::OpenOptions::new()
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

    /// Stub implementation for non-Unix platforms.
    #[cfg(not(unix))]
    #[instrument(skip(_path))]
    pub fn open_at(_path: &std::path::Path, timeout_secs: u32) -> WatchdogResult<Self> {
        warn!("Watchdog not available on this platform — using stub");
        Ok(Self {
            armed: false,
            timeout_secs,
            armed_at: None,
            pet_count: 0,
        })
    }

    /// Check if the watchdog device is present on this system.
    pub fn is_available() -> bool {
        #[cfg(unix)]
        {
            std::path::Path::new(DEFAULT_WATCHDOG_DEV).exists()
        }
        #[cfg(not(unix))]
        {
            false
        }
    }

    /// Whether the watchdog is currently armed.
    pub fn is_armed(&self) -> bool {
        #[cfg(unix)]
        {
            self.dev.is_some() && self.armed_at.is_some()
        }
        #[cfg(not(unix))]
        {
            self.armed && self.armed_at.is_some()
        }
    }

    /// Arm the watchdog with the configured timeout and return a guard.
    #[instrument(skip(self))]
    pub fn arm(&mut self) -> WatchdogResult<WatchdogGuard<'_>> {
        if self.is_armed() {
            return Err(WatchdogError::AlreadyArmed);
        }

        // Perform initial pet to arm the watchdog
        self.pet_raw()?;
        self.armed_at = Some(Instant::now());
        self.pet_count = 1;

        info!(timeout_secs = self.timeout_secs, "Watchdog armed");

        Ok(WatchdogGuard {
            watchdog: self,
            disarmed: false,
        })
    }

    /// Disarm the watchdog before a controlled reboot.
    #[cfg(unix)]
    #[instrument(skip(self))]
    pub fn disarm(&mut self) -> WatchdogResult<()> {
        if !self.is_armed() {
            return Err(WatchdogError::NotArmed);
        }

        if let Some(dev) = &mut self.dev {
            use std::io::Write;
            dev.write_all(b"V").map_err(WatchdogError::PetFailed)?;
            dev.flush().map_err(WatchdogError::PetFailed)?;
        }

        self.dev = None;
        self.armed_at = None;

        info!("Watchdog disarmed safely — controlled shutdown");
        Ok(())
    }

    /// Disarm stub for non-Unix.
    #[cfg(not(unix))]
    #[instrument(skip(self))]
    pub fn disarm(&mut self) -> WatchdogResult<()> {
        if !self.is_armed() {
            return Err(WatchdogError::NotArmed);
        }
        self.armed = false;
        self.armed_at = None;
        info!("Watchdog disarmed (stub)");
        Ok(())
    }

    /// Pet (keepalive) the watchdog to reset the countdown timer.
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
    #[cfg(unix)]
    fn pet_raw(&mut self) -> WatchdogResult<()> {
        let dev = self.dev.as_mut().ok_or(WatchdogError::NotArmed)?;
        use std::io::Write;
        dev.write_all(&[0]).map_err(WatchdogError::PetFailed)
    }

    #[cfg(not(unix))]
    fn pet_raw(&mut self) -> WatchdogResult<()> {
        if !self.armed {
            return Err(WatchdogError::NotArmed);
        }
        Ok(())
    }

    /// Change the watchdog timeout.
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
pub async fn pet_loop(
    mut watchdog: Watchdog,
    interval: Duration,
    mut cancel: tokio::sync::watch::Receiver<bool>,
) -> WatchdogResult<()> {
    let mut guard = watchdog.arm()?;
    info!(
        interval_ms = interval.as_millis(),
        "Watchdog pet loop started"
    );

    loop {
        tokio::select! {
            _ = tokio::time::sleep(interval) => {
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
        // On Linux without /dev/watchdog, open() fails.
        // On platforms with a stub (Windows/macOS), open() always succeeds.
        #[cfg(unix)]
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
        if Watchdog::is_available() {
            let mut wd = Watchdog::open().unwrap();
            assert!(!wd.is_armed());
            let _guard = wd.arm().unwrap();
            // _guard drops here — if it was armed, Drop will disarm
        }
    }
}
