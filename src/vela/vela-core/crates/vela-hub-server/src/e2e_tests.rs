//! Full system E2E integration test: Hub server + device attestation
//! + rollout creation + FlashPack download pipeline.

use axum::{
    Router,
    routing::{get, post},
};
use std::sync::Arc;

use crate::routes;
use crate::state::AppState;

/// Build the Hub router with shared state (for in-process testing).
fn build_app(state: Arc<AppState>) -> Router {
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

async fn spawn_test_server(
    app: Router,
) -> (
    String,
    tokio::sync::oneshot::Sender<()>,
    tokio::task::JoinHandle<()>,
) {
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel();

    let server = tokio::spawn(async move {
        axum::serve(listener, app)
            .with_graceful_shutdown(async move {
                let _ = shutdown_rx.await;
            })
            .await
            .unwrap();
    });

    (format!("http://{addr}"), shutdown_tx, server)
}

#[tokio::test]
async fn test_e2e_health_check() {
    let state = Arc::new(AppState::new());
    let app = build_app(state.clone());
    let (base_url, shutdown_tx, server) = spawn_test_server(app).await;

    let resp = reqwest::get(format!("{base_url}/api/v1/health"))
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);
    let body: serde_json::Value = resp.json().await.unwrap();
    assert_eq!(body["status"], "ok");
    assert_eq!(body["service"], "vela-hub");

    let _ = shutdown_tx.send(());
    server.await.unwrap();
}

#[tokio::test]
async fn test_e2e_device_attestation_and_poll() {
    let state = Arc::new(AppState::new());
    let app = build_app(state.clone());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });

    let client = reqwest::Client::new();

    // Step 1: Device attests
    let resp = client
        .post(format!("http://{addr}/api/v1/attest"))
        .json(&serde_json::json!({
            "device_id": "device-001",
            "model": "vela-gateway-v2",
            "hardware_fingerprint": "fp-abc123"
        }))
        .send()
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);
    let body: serde_json::Value = resp.json().await.unwrap();
    assert_eq!(body["status"], "attested");
    assert_eq!(body["device_id"], "device-001");
    assert!(body["session_token"].is_string());

    // Step 2: Device polls — no update yet
    let resp = client
        .get(format!("http://{addr}/api/v1/rollout/poll"))
        .query(&[("device_id", "device-001"), ("current_version", "1.0.0")])
        .send()
        .await
        .unwrap();
    let body: serde_json::Value = resp.json().await.unwrap();
    assert_eq!(body["status"], "no_update");

    // Step 3: Device sends heartbeat
    let resp = client
        .post(format!("http://{addr}/api/v1/heartbeat"))
        .json(&serde_json::json!({
            "device_id": "device-001",
            "current_version": "1.0.0",
            "lifecycle_phase": "idle",
            "health_ok": true
        }))
        .send()
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);

    // Step 4: Verify device is listed
    let resp = client
        .get(format!("http://{addr}/api/v1/devices"))
        .send()
        .await
        .unwrap();
    let devices: Vec<serde_json::Value> = resp.json().await.unwrap();
    assert_eq!(devices.len(), 1);
    assert_eq!(devices[0]["device_id"], "device-001");
    assert_eq!(devices[0]["model"], "vela-gateway-v2");
}

#[tokio::test]
async fn test_e2e_rollout_creation_and_poll() {
    let state = Arc::new(AppState::new());

    // Pre-register an artifact
    {
        let mut artifacts = state.artifacts.write().await;
        artifacts.insert(
            "artifact-001".into(),
            crate::state::ArtifactRecord {
                artifact_id: "artifact-001".into(),
                bundle_name: "vela-os".into(),
                bundle_version: "2.0.0".into(),
                format_version: "1.0.0".into(),
                payload_type: "full_image".into(),
                size_bytes: 1048576,
                checksum: "sha256:abc123".into(),
                created_at: "2026-01-01T00:00:00Z".into(),
                file_path: "/tmp/test.fpk".into(),
            },
        );
        // Create a small test artifact file
        std::fs::create_dir_all("/tmp").ok();
        std::fs::write("/tmp/test.fpk", b"fake-flashpack-data-vela-ota").unwrap();
    }

    let app = build_app(state.clone());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });

    let client = reqwest::Client::new();

    // Create a rollout
    let resp = client
        .post(format!("http://{addr}/api/v1/rollouts"))
        .json(&serde_json::json!({
            "artifact_id": "artifact-001",
            "target_version": "2.0.0",
            "min_version": "1.0.0",
            "force_install": false
        }))
        .send()
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);
    let body: serde_json::Value = resp.json().await.unwrap();
    assert_eq!(body["status"], "active");
    let rollout_id = body["rollout_id"].as_str().unwrap().to_string();

    // Attest a device
    client
        .post(format!("http://{addr}/api/v1/attest"))
        .json(&serde_json::json!({
            "device_id": "device-002",
            "model": "vela-gateway-v3"
        }))
        .send()
        .await
        .unwrap();

    // Device polls — should get update
    let resp = client
        .get(format!("http://{addr}/api/v1/rollout/poll"))
        .query(&[("device_id", "device-002"), ("current_version", "1.5.0")])
        .send()
        .await
        .unwrap();
    let body: serde_json::Value = resp.json().await.unwrap();
    assert_eq!(body["status"], "update_available");
    assert_eq!(body["rollout_id"], rollout_id);
    assert_eq!(body["target_version"], "2.0.0");
    assert_eq!(body["flashpack_size"], 1048576);

    // Download the artifact
    let resp = client
        .get(format!("http://{addr}/api/v1/artifacts/artifact-001"))
        .send()
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);
    let data = resp.bytes().await.unwrap();
    assert_eq!(&data[..], b"fake-flashpack-data-vela-ota");
}

#[tokio::test]
async fn test_e2e_rollout_with_nonexistent_artifact() {
    let state = Arc::new(AppState::new());
    let app = build_app(state.clone());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });

    let client = reqwest::Client::new();

    let resp = client
        .post(format!("http://{addr}/api/v1/rollouts"))
        .json(&serde_json::json!({
            "artifact_id": "nonexistent",
            "target_version": "2.0.0"
        }))
        .send()
        .await
        .unwrap();
    assert_eq!(resp.status(), 200);
    let body: serde_json::Value = resp.json().await.unwrap();
    assert!(body.get("error").is_some());
}

#[tokio::test]
async fn test_e2e_multiple_devices() {
    let state = Arc::new(AppState::new());
    let app = build_app(state.clone());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });

    let client = reqwest::Client::new();

    // Register 3 devices
    for id in &["d-a", "d-b", "d-c"] {
        client
            .post(format!("http://{addr}/api/v1/attest"))
            .json(&serde_json::json!({
                "device_id": id,
                "model": "vela-gateway-v2"
            }))
            .send()
            .await
            .unwrap();
    }

    let resp = client
        .get(format!("http://{addr}/api/v1/devices"))
        .send()
        .await
        .unwrap();
    let devices: Vec<serde_json::Value> = resp.json().await.unwrap();
    assert_eq!(devices.len(), 3);
}

#[tokio::test]
async fn test_e2e_device_version_tracking() {
    let state = Arc::new(AppState::new());
    let app = build_app(state.clone());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    tokio::spawn(async move {
        axum::serve(listener, app).await.unwrap();
    });

    let client = reqwest::Client::new();

    // Attest
    client
        .post(format!("http://{addr}/api/v1/attest"))
        .json(&serde_json::json!({
            "device_id": "dev-v",
            "model": "test"
        }))
        .send()
        .await
        .unwrap();

    // Poll with version 1.0
    client
        .get(format!("http://{addr}/api/v1/rollout/poll"))
        .query(&[("device_id", "dev-v"), ("current_version", "1.0.0")])
        .send()
        .await
        .unwrap();

    // Heartbeat with updated version
    client
        .post(format!("http://{addr}/api/v1/heartbeat"))
        .json(&serde_json::json!({
            "device_id": "dev-v",
            "current_version": "2.0.0",
            "health_ok": true
        }))
        .send()
        .await
        .unwrap();

    // Verify version updated
    let resp = client
        .get(format!("http://{addr}/api/v1/devices"))
        .send()
        .await
        .unwrap();
    let devices: Vec<serde_json::Value> = resp.json().await.unwrap();
    let dev = devices.iter().find(|d| d["device_id"] == "dev-v").unwrap();
    assert_eq!(dev["current_version"], "2.0.0");
}
