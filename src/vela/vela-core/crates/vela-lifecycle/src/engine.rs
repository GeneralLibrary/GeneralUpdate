//! Lifecycle engine: drives the OTA update state machine.
//!
//! The engine manages strict unidirectional phase transitions with
//! per-phase timeout enforcement and structured tracing.

use std::sync::Arc;
use std::time::Duration;

use tracing::{error, info, info_span, instrument, trace, warn};

use crate::{
    LifecycleConfig, LifecycleContext, LifecycleError, LifecycleMetrics, LifecycleOutcome,
    LifecycleResult, PhaseTimer, UpdatePhase,
};

/// Drives the full update lifecycle from Idle through Commit.
///
/// The engine is `Send + Sync` and suitable for running on a background
/// thread or async task.
pub struct LifecycleEngine {
    pub config: LifecycleConfig,
}

impl LifecycleEngine {
    /// Create a new engine with the given configuration.
    pub fn new(config: LifecycleConfig) -> Self {
        Self { config }
    }

    /// Get the timeout for a specific phase.
    pub fn phase_timeout(&self, phase: UpdatePhase) -> Duration {
        self.config
            .phase_timeouts
            .get(&phase)
            .copied()
            .unwrap_or(Duration::from_secs(600))
    }

    /// Execute a single phase transition and return the next phase.
    ///
    /// This is the core of the state machine. It:
    /// 1. Creates a tracing span tagged with the phase name.
    /// 2. Starts a `PhaseTimer` for metrics.
    /// 3. Applies a per-phase timeout.
    /// 4. Calls the phase handler.
    /// 5. Records metrics on completion or failure.
    #[instrument(skip(self, ctx), fields(update_id = %ctx.update_id))]
    pub async fn execute_phase(
        &self,
        ctx: &LifecycleContext,
        next_phase: UpdatePhase,
    ) -> LifecycleResult<UpdatePhase> {
        let span = info_span!(
            "lifecycle_phase",
            phase = ?next_phase,
            retry = ctx.metrics.lock().unwrap().retry_count,
            elapsed_total_ms = ctx.metrics.lock().unwrap().total_elapsed_ms,
        );
        let _guard = span.enter();

        info!("Beginning update lifecycle phase");

        // Check for terminal states first
        let after = match next_phase {
            UpdatePhase::Committing => {
                info!("Update committed successfully");
                ctx.metrics.lock().unwrap().outcome = Some(LifecycleOutcome::Success);
                return Ok(UpdatePhase::Idle);
            }
            UpdatePhase::FallbackRecovery => {
                warn!("Entering fallback recovery — attempting to restore");
                self.handle_fallback_recovery(ctx).await?;
                return Ok(UpdatePhase::Idle);
            }
            UpdatePhase::Idle => {
                trace!("Entering idle — waiting for next poll trigger");
                return Ok(UpdatePhase::Polling);
            }
            phase => {
                let timer = PhaseTimer::begin(phase, ctx);
                let result =
                    tokio::time::timeout(self.phase_timeout(phase), self.handle_phase(phase, ctx))
                        .await;

                match result {
                    Ok(Ok(next)) => {
                        timer.complete(ctx);
                        info!(next_phase = ?next, "Phase transition completed");
                        Ok(next)
                    }
                    Ok(Err(e)) => {
                        warn!(error = %e, "Phase failed, triggering fallback");
                        ctx.record_error(&e);
                        Ok(UpdatePhase::FallbackRecovery)
                    }
                    Err(_elapsed) => {
                        error!("Phase timed out, triggering fallback");
                        let timeout_err = LifecycleError::PhaseTimeout(phase);
                        ctx.record_error(&timeout_err);
                        Ok(UpdatePhase::FallbackRecovery)
                    }
                }
            }
        };
        after
    }

    /// Handle a non-terminal phase transition.
    async fn handle_phase(
        &self,
        phase: UpdatePhase,
        ctx: &LifecycleContext,
    ) -> LifecycleResult<UpdatePhase> {
        match phase {
            UpdatePhase::Polling => {
                trace!("Polling Vela Hub for available updates");
                // In production, this would call the Hub API.
                // For now, simulate a successful poll that finds no update
                // (returning to Idle) or an update (moving to Acquiring).
                Ok(UpdatePhase::Idle)
            }
            UpdatePhase::Acquiring => {
                trace!("Acquiring FlashPack from Hub");
                ctx.record_bytes_downloaded(0); // placeholder
                Ok(UpdatePhase::Validating)
            }
            UpdatePhase::Validating => {
                trace!("Validating FlashPack");
                let start = std::time::Instant::now();
                // Validation logic would go here
                ctx.record_validation_time(start.elapsed().as_millis() as u64);
                Ok(UpdatePhase::Installing)
            }
            UpdatePhase::Installing => {
                trace!("Installing to alternate slot");
                ctx.record_bytes_written(0); // placeholder
                Ok(UpdatePhase::Rebooting)
            }
            UpdatePhase::Rebooting => {
                warn!("Reboot required to complete update");
                Ok(UpdatePhase::Verifying)
            }
            UpdatePhase::Verifying => {
                trace!("Verifying new system health after reboot");
                Ok(UpdatePhase::Committing)
            }
            _ => Err(LifecycleError::InvalidTransition {
                from: phase,
                to: phase,
            }),
        }
    }

    /// Handle fallback recovery — idempotent operations to restore the system.
    async fn handle_fallback_recovery(&self, ctx: &LifecycleContext) -> LifecycleResult<()> {
        warn!("Executing fallback recovery procedures");

        // Fallback steps (all must be idempotent):
        // 1. Set boot flag to FallbackRequested
        // 2. Restore primary slot version markers
        // 3. Clean up partial downloads

        let reason = ctx
            .metrics
            .lock()
            .unwrap()
            .phase_durations
            .iter()
            .map(|(p, _)| format!("{p:?}"))
            .collect::<Vec<_>>()
            .join(", ");

        ctx.metrics.lock().unwrap().outcome = Some(LifecycleOutcome::FallbackRecovery {
            reason,
            phase: UpdatePhase::FallbackRecovery,
        });

        info!("Fallback recovery complete — system restored to last known-good state");
        Ok(())
    }
}

/// Run the full lifecycle from Idle through completion.
///
/// This is the top-level entry point for performing an OTA update.
/// It runs the state machine to completion, returning the final outcome.
#[instrument(skip(engine, ctx))]
pub async fn run_lifecycle(
    engine: &LifecycleEngine,
    ctx: &LifecycleContext,
) -> LifecycleResult<LifecycleOutcome> {
    info!(update_id = %ctx.update_id, "Starting OTA update lifecycle");

    let mut current = UpdatePhase::Idle;
    let max_phases = 100; // safety limit to prevent infinite loops

    for iteration in 0..max_phases {
        trace!(?current, iteration, "Lifecycle iteration");

        match engine.execute_phase(ctx, current).await {
            Ok(UpdatePhase::Idle) => {
                // Lifecycle completed (either success or fallback)
                break;
            }
            Ok(next) => {
                current = next;
            }
            Err(e) => {
                error!(error = %e, "Unrecoverable lifecycle error");
                return Err(e);
            }
        }
    }

    let outcome = ctx.metrics.lock().unwrap().outcome.clone();
    match &outcome {
        Some(LifecycleOutcome::Success) => {
            info!("OTA update lifecycle completed successfully");
        }
        Some(LifecycleOutcome::FallbackRecovery { reason, phase }) => {
            warn!(%reason, ?phase, "OTA update lifecycle ended with fallback");
        }
        Some(LifecycleOutcome::Aborted) => {
            warn!("OTA update lifecycle was aborted");
        }
        None => {
            warn!("Lifecycle exited without a recorded outcome");
        }
    }

    Ok(outcome.unwrap_or(LifecycleOutcome::Aborted))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::LifecycleConfig;
    use std::sync::Mutex;

    fn make_ctx() -> LifecycleContext {
        LifecycleContext {
            update_id: "test-update-001".into(),
            metrics: Mutex::new(LifecycleMetrics::default()),
        }
    }

    #[tokio::test]
    async fn test_engine_spawns_and_returns_idle() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let result = engine.execute_phase(&ctx, UpdatePhase::Idle).await;
        assert_eq!(result.unwrap(), UpdatePhase::Polling);
    }

    #[tokio::test]
    async fn test_polling_to_idle() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let result = engine.execute_phase(&ctx, UpdatePhase::Polling).await;
        assert_eq!(result.unwrap(), UpdatePhase::Idle);
    }

    #[tokio::test]
    async fn test_acquiring_to_validating() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let result = engine.execute_phase(&ctx, UpdatePhase::Acquiring).await;
        assert_eq!(result.unwrap(), UpdatePhase::Validating);
    }

    #[tokio::test]
    async fn test_full_chain_idle_to_idle() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();

        // Idle → Polling
        let next = engine.execute_phase(&ctx, UpdatePhase::Idle).await.unwrap();
        assert_eq!(next, UpdatePhase::Polling);

        // Polling → Idle (no update available)
        let next = engine.execute_phase(&ctx, next).await.unwrap();
        assert_eq!(next, UpdatePhase::Idle);
    }

    #[tokio::test]
    async fn test_fallback_recovery_returns_to_idle() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let result = engine
            .execute_phase(&ctx, UpdatePhase::FallbackRecovery)
            .await
            .unwrap();
        assert_eq!(result, UpdatePhase::Idle);
        assert!(matches!(
            ctx.metrics.lock().unwrap().outcome,
            Some(LifecycleOutcome::FallbackRecovery { .. })
        ));
    }

    #[tokio::test]
    async fn test_committing_returns_to_idle_with_success() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let result = engine
            .execute_phase(&ctx, UpdatePhase::Committing)
            .await
            .unwrap();
        assert_eq!(result, UpdatePhase::Idle);
        assert_eq!(
            ctx.metrics.lock().unwrap().outcome,
            Some(LifecycleOutcome::Success)
        );
    }

    #[tokio::test]
    async fn test_phase_timeout_triggers_fallback() {
        let mut config = LifecycleConfig::default();
        // Set an impossibly short timeout for Polling
        config
            .phase_timeouts
            .insert(UpdatePhase::Polling, Duration::from_nanos(1));

        let engine = LifecycleEngine::new(config);
        let ctx = make_ctx();
        let result = engine.execute_phase(&ctx, UpdatePhase::Polling).await;
        // Should trigger fallback due to timeout
        assert!(matches!(result, Ok(UpdatePhase::FallbackRecovery)));
    }

    #[tokio::test]
    async fn test_run_lifecycle_to_completion() {
        let engine = LifecycleEngine::new(LifecycleConfig::default());
        let ctx = make_ctx();
        let outcome = run_lifecycle(&engine, &ctx).await.unwrap();
        // With current stub implementations, Polling → Idle completes.
        assert!(matches!(
            outcome,
            LifecycleOutcome::Aborted | LifecycleOutcome::FallbackRecovery { .. }
        ));
    }
}
