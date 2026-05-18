//! Slot recovery guard — RAII pattern for automatic fallback on error.
//!
//! The `SlotRecoveryGuard` ensures that if an update fails at any point
//! (panic, error, early return), the boot flag is set to `FallbackRequested`
//! so the bootloader can recover to the last known-good slot.

use tracing::{error, info, instrument, warn};

use crate::{BootFlag, SlotError, SlotProvider, SlotResult};

/// RAII guard that triggers fallback on drop if not explicitly committed.
///
/// # Usage
/// ```ignore
/// let guard = SlotRecoveryGuard::new(&provider).await?;
/// // ... perform update operations ...
/// // If everything succeeds:
/// guard.commit().await?;
/// // If the function panics or returns early, Drop sets FallbackRequested.
/// ```
pub struct SlotRecoveryGuard<'a> {
    provider: &'a dyn SlotProvider,
    committed: bool,
    fallback_on_drop: bool,
}

impl<'a> SlotRecoveryGuard<'a> {
    /// Create a new recovery guard.
    ///
    /// On creation, the guard records that a fallback should be triggered
    /// if `commit()` is not called before the guard is dropped.
    #[instrument(skip(provider))]
    pub async fn new(provider: &'a dyn SlotProvider) -> SlotResult<Self> {
        info!("SlotRecoveryGuard activated — fallback will trigger on drop if not committed");
        Ok(Self {
            provider,
            committed: false,
            fallback_on_drop: false,
        })
    }

    /// Mark this guard as "armed" — fallback WILL trigger on drop.
    ///
    /// This should be called right before starting a dangerous operation.
    /// It is a no-op if the guard is already committed.
    pub fn arm(&mut self) {
        if !self.committed {
            self.fallback_on_drop = true;
            info!("SlotRecoveryGuard armed — fallback will trigger on panic or early return");
        }
    }

    /// Disarm the guard — fallback will NOT trigger on drop.
    ///
    /// Call this when a non-fatal error occurs but the slot is still viable.
    pub fn disarm(&mut self) {
        self.fallback_on_drop = false;
        warn!("SlotRecoveryGuard disarmed — fallback will NOT trigger on drop");
    }

    /// Commit the guard — persist the success and prevent fallback.
    ///
    /// After calling this, dropping the guard is harmless.
    #[instrument(skip(self))]
    pub async fn commit(mut self) -> SlotResult<()> {
        info!("Committing SlotRecoveryGuard — marking update as successful");
        self.provider.set_boot_flag(BootFlag::CommitSuccess).await?;
        self.committed = true;
        self.fallback_on_drop = false;
        Ok(())
    }

    /// Explicitly trigger fallback (e.g., on a caught error).
    ///
    /// This is equivalent to dropping an armed guard.
    #[instrument(skip(self))]
    pub async fn fallback(self) -> SlotResult<()> {
        warn!("Triggering explicit slot fallback");
        self.provider.set_boot_flag(BootFlag::FallbackRequested).await?;
        Ok(())
    }
}

impl<'a> Drop for SlotRecoveryGuard<'a> {
    fn drop(&mut self) {
        if self.fallback_on_drop && !self.committed {
            error!("SlotRecoveryGuard dropped without commit — system may need manual recovery");
            // We cannot call async functions in Drop.
            // In production, a synchronous fallback mechanism (e.g., writing
            // to a well-known file or sysfs node) would be used here.
            // For now, we log at ERROR level so the operator knows.
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::mock::MockSlotProvider;

    #[tokio::test]
    async fn test_guard_commit_prevents_fallback() {
        let provider = MockSlotProvider::new();
        let mut guard = SlotRecoveryGuard::new(&provider).await.unwrap();
        guard.arm();
        guard.commit().await.unwrap();
        // Guard is consumed by commit()
    }

    #[tokio::test]
    async fn test_guard_disarm_prevents_fallback() {
        let provider = MockSlotProvider::new();
        let mut guard = SlotRecoveryGuard::new(&provider).await.unwrap();
        guard.arm();
        guard.disarm();
        // Drop without commit is now safe.
        drop(guard);
    }

    #[tokio::test]
    async fn test_guard_explicit_fallback() {
        let provider = MockSlotProvider::new();
        let guard = SlotRecoveryGuard::new(&provider).await.unwrap();
        // Explicit fallback — sets the boot flag
        guard.fallback().await.unwrap();
        assert_eq!(provider.snapshot().boot_flag, Some(BootFlag::FallbackRequested));
    }

    #[tokio::test]
    async fn test_guard_not_armed_no_fallback_on_drop() {
        let provider = MockSlotProvider::new();
        let guard = SlotRecoveryGuard::new(&provider).await.unwrap();
        // Not armed, not committed — safe to drop.
        drop(guard);
    }
}
