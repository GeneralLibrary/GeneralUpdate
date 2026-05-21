//! Vela E2E Integration Tests
//!
//! Cross-crate integration tests validating that Vela OTA subsystems
//! compose correctly and the full update pipeline is robust.
//!
//! ## Test Suites
//!
//! 1. Watchdog + EventBus — event emission, subscriber delivery, history
//! 2. Slot Manager + Lifecycle — slot transitions, lifecycle phases, metrics
//! 3. Hub Client + Retry + Download — retry strategy, checksum, auth
//! 4. Full Pipeline — complete state transitions, terminal states
//! 5. Error Recovery — corrupt data, network errors, timeout fallback
//! 6. Configuration Validation — default configs, custom configs

// Suite modules
#[cfg(test)]
mod suite1_watchdog_bus;
#[cfg(test)]
mod suite2_slot_lifecycle;
#[cfg(test)]
mod suite3_hub_retry;
#[cfg(test)]
mod suite4_pipeline;
#[cfg(test)]
mod suite5_error_recovery;
#[cfg(test)]
mod suite6_config;

// Ensure workspace crate references compile
use vela_attestation as _;
use vela_core as _;
use vela_flashpack as _;
use vela_hub as _;
use vela_lifecycle as _;
use vela_pulse as _;
use vela_slotmgr as _;
use vela_watchdog as _;
