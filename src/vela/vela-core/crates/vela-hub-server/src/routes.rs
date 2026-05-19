//! Route handlers for Vela Hub REST API.

use axum::{
    Json,
    extract::{Path, Query, State},
    http::StatusCode,
};
use std::sync::Arc;

use crate::state::{AppState, DeviceRecord, DeviceStatus};

/// GET /api/v1/health
pub async fn health() -> Json<serde_json::Value> {
    Json(serde_json::json!({
        "status": "ok",
        "service": "vela-hub",
        "version": env!("CARGO_PKG_VERSION"),
    }))
}

/// GET /api/v1/rollout/poll?device_id=&current_version=
#[derive(serde::Deserialize)]
pub struct PollQuery {
    pub device_id: String,
    pub current_version: String,
}

pub async fn poll_for_update(
    State(state): State<Arc<AppState>>,
    Query(q): Query<PollQuery>,
) -> Json<serde_json::Value> {
    // Update last seen timestamp
    {
        let mut devices = state.devices.write().await;
        if let Some(dev) = devices.get_mut(&q.device_id) {
            dev.last_seen = chrono::Utc::now().to_rfc3339();
            dev.current_version = q.current_version.clone();
            dev.status = DeviceStatus::Online;
        }
    }

    // Check for active rollouts matching this device/version
    let rollouts = state.rollouts.read().await;
    for rollout in rollouts.values() {
        if rollout.status == crate::state::RolloutStatus::Active
            && q.current_version >= rollout.min_version
            && q.current_version < rollout.target_version
        {
            // Get artifact info
            let artifacts = state.artifacts.read().await;
            if let Some(artifact) = artifacts.get(&rollout.artifact_id) {
                return Json(serde_json::json!({
                    "status": "update_available",
                    "rollout_id": rollout.rollout_id,
                    "flashpack_url": format!("/api/v1/artifacts/{}", artifact.artifact_id),
                    "flashpack_checksum": artifact.checksum,
                    "flashpack_size": artifact.size_bytes,
                    "target_version": rollout.target_version,
                    "force_install": rollout.force_install,
                    "release_notes": null,
                }));
            }
        }
    }

    Json(serde_json::json!({
        "status": "no_update"
    }))
}

/// POST /api/v1/attest
#[derive(serde::Deserialize)]
pub struct AttestRequest {
    pub device_id: String,
    pub model: String,
    pub hardware_fingerprint: Option<String>,
}

pub async fn attest(
    State(state): State<Arc<AppState>>,
    Json(req): Json<AttestRequest>,
) -> Json<serde_json::Value> {
    let now = chrono::Utc::now().to_rfc3339();
    let mut devices = state.devices.write().await;

    devices
        .entry(req.device_id.clone())
        .and_modify(|d| {
            d.last_seen = now.clone();
            d.attested_at = Some(now.clone());
            d.status = DeviceStatus::Online;
        })
        .or_insert(DeviceRecord {
            device_id: req.device_id.clone(),
            model: req.model,
            current_version: "unknown".into(),
            last_seen: now,
            status: DeviceStatus::Online,
            attested_at: Some(chrono::Utc::now().to_rfc3339()),
        });

    Json(serde_json::json!({
        "status": "attested",
        "device_id": req.device_id,
        "session_token": uuid::Uuid::new_v4().to_string(),
    }))
}

/// POST /api/v1/heartbeat
#[derive(serde::Deserialize)]
pub struct HeartbeatRequest {
    pub device_id: String,
    pub current_version: String,
    pub lifecycle_phase: Option<String>,
    pub health_ok: bool,
}

pub async fn heartbeat(
    State(state): State<Arc<AppState>>,
    Json(req): Json<HeartbeatRequest>,
) -> Json<serde_json::Value> {
    let now = chrono::Utc::now().to_rfc3339();
    let mut devices = state.devices.write().await;

    if let Some(dev) = devices.get_mut(&req.device_id) {
        dev.last_seen = now;
        dev.current_version = req.current_version;
        dev.status = if req.health_ok {
            DeviceStatus::Online
        } else {
            DeviceStatus::Unknown
        };
    }

    Json(serde_json::json!({
        "status": "acknowledged",
        "sequence": chrono::Utc::now().timestamp(),
    }))
}

/// GET /api/v1/devices
pub async fn list_devices(State(state): State<Arc<AppState>>) -> Json<Vec<DeviceRecord>> {
    let devices = state.devices.read().await;
    Json(devices.values().cloned().collect())
}

/// POST /api/v1/rollouts
#[derive(serde::Deserialize)]
pub struct CreateRolloutRequest {
    pub artifact_id: String,
    pub target_version: String,
    pub min_version: Option<String>,
    pub force_install: Option<bool>,
}

pub async fn create_rollout(
    State(state): State<Arc<AppState>>,
    Json(req): Json<CreateRolloutRequest>,
) -> (StatusCode, Json<serde_json::Value>) {
    // Validate artifact exists
    {
        let artifacts = state.artifacts.read().await;
        if !artifacts.contains_key(&req.artifact_id) {
            return (
                StatusCode::NOT_FOUND,
                Json(serde_json::json!({
                    "error": "artifact not found",
                    "artifact_id": req.artifact_id
                })),
            );
        }
    }

    let rollout_id = uuid::Uuid::new_v4().to_string();
    let rollout = crate::state::RolloutRecord {
        rollout_id: rollout_id.clone(),
        artifact_id: req.artifact_id,
        target_version: req.target_version,
        min_version: req.min_version.unwrap_or_else(|| "0.0.0".into()),
        force_install: req.force_install.unwrap_or(false),
        created_at: chrono::Utc::now().to_rfc3339(),
        status: crate::state::RolloutStatus::Active,
    };

    state
        .rollouts
        .write()
        .await
        .insert(rollout_id.clone(), rollout);

    (
        StatusCode::OK,
        Json(serde_json::json!({
            "rollout_id": rollout_id,
            "status": "active"
        })),
    )
}

/// GET /api/v1/artifacts/:id
pub async fn download_artifact(
    State(state): State<Arc<AppState>>,
    Path(id): Path<String>,
) -> Result<Vec<u8>, (axum::http::StatusCode, String)> {
    let artifacts = state.artifacts.read().await;
    let artifact = artifacts.get(&id).ok_or((
        axum::http::StatusCode::NOT_FOUND,
        format!("Artifact {id} not found"),
    ))?;

    std::fs::read(&artifact.file_path).map_err(|e| {
        (
            axum::http::StatusCode::INTERNAL_SERVER_ERROR,
            format!("Failed to read artifact: {e}"),
        )
    })
}
