//! Vela Hub — OTA device management server.
//!
//! Provides REST API endpoints for device registration, rollout
//! deployment, FlashPack artifact distribution, and health monitoring.

use axum::{
    Router,
    routing::{get, post},
};
use std::sync::Arc;
use tracing::info;

#[cfg(test)]
mod e2e_tests;

mod routes;
mod state;

use state::AppState;

/// Build the Hub router with shared state.
///
/// Used by both the production `main` entry point and the in-process E2E tests
/// so that both always exercise the same set of routes.
pub fn build_router(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/api/v1/health", get(routes::health))
        .route("/api/v1/rollout/poll", get(routes::poll_for_update))
        .route("/api/v1/attest", post(routes::attest))
        .route("/api/v1/heartbeat", post(routes::heartbeat))
        .route("/api/v1/devices", get(routes::list_devices))
        .route("/api/v1/rollouts", post(routes::create_rollout))
        .route("/api/v1/artifacts/{id}", get(routes::download_artifact))
        .with_state(state)
}

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
        .with_env_filter("vela_hub=info,axum=info")
        .with_target(false)
        .init();

    let state = Arc::new(AppState::new());
    let app = build_router(state);

    let addr = "0.0.0.0:8080";
    info!("Vela Hub starting on http://{addr}");

    let listener = tokio::net::TcpListener::bind(addr).await.unwrap();
    axum::serve(listener, app).await.unwrap();
}
