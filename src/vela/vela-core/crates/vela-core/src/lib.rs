#![forbid(unsafe_code)]
#![doc = "Vela Core: top-level orchestration crate for the Vela OTA system."]

pub mod orchestrator;

use tracing_subscriber::{fmt, prelude::*, EnvFilter};

/// Initialize structured JSON logging for the Vela OTA system.
pub fn init_logging(verbose: bool) {
    let filter = if verbose {
        EnvFilter::new("vela=trace")
    } else {
        EnvFilter::new("vela=info")
    };

    let subscriber = fmt::layer()
        .json()
        .with_current_span(true)
        .with_span_list(true)
        .with_target(true);

    tracing_subscriber::registry()
        .with(filter)
        .with(subscriber)
        .init();

    tracing::info!("Vela Core logging initialized (verbose={verbose})");
}

/// Top-level error type for the Vela Core orchestration layer.
#[derive(Debug, thiserror::Error)]
pub enum VelaError {
    #[error("Initialization failed: {0}")]
    InitError(String),

    #[error("FlashPack error: {0}")]
    FlashPack(#[from] vela_flashpack::FlashPackError),

    #[error("Lifecycle error: {0}")]
    Lifecycle(#[from] vela_lifecycle::LifecycleError),

    #[error("Slot error: {0}")]
    Slot(#[from] vela_slotmgr::SlotError),

    #[error("Attestation error: {0}")]
    Attestation(#[from] vela_attestation::AttestationError),

    #[error("Pulse error: {0}")]
    Pulse(#[from] vela_pulse::PulseError),

    #[error("Hub error: {0}")]
    Hub(#[from] vela_hub::HubError),

    #[error("Watchdog error: {0}")]
    Watchdog(#[from] vela_watchdog::WatchdogError),

    #[error("Orchestrator error: {0}")]
    Orchestrator(String),
}

/// Result type alias for Vela Core operations.
pub type VelaResult<T> = Result<T, VelaError>;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_logging_init() {
        init_logging(false);
        tracing::info!("Logging test passed");
    }
}
