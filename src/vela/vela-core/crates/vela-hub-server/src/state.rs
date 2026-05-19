//! Shared application state for Vela Hub.

use chrono::Utc;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;

/// In-memory application state with thread-safe access.
pub struct AppState {
    pub devices: RwLock<HashMap<String, DeviceRecord>>,
    pub rollouts: RwLock<HashMap<String, RolloutRecord>>,
    pub artifacts: RwLock<HashMap<String, ArtifactRecord>>,
}

impl AppState {
    pub fn new() -> Self {
        Self {
            devices: RwLock::new(HashMap::new()),
            rollouts: RwLock::new(HashMap::new()),
            artifacts: RwLock::new(HashMap::new()),
        }
    }
}

/// Registered device record.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DeviceRecord {
    pub device_id: String,
    pub model: String,
    pub current_version: String,
    pub last_seen: String,
    pub status: DeviceStatus,
    pub attested_at: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum DeviceStatus {
    Online,
    Offline,
    Updating,
    Unknown,
}

/// Rollout deployment record.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RolloutRecord {
    pub rollout_id: String,
    pub artifact_id: String,
    pub target_version: String,
    pub min_version: String,
    pub force_install: bool,
    pub created_at: String,
    pub status: RolloutStatus,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RolloutStatus {
    Draft,
    Active,
    Paused,
    Completed,
}

/// FlashPack artifact metadata.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ArtifactRecord {
    pub artifact_id: String,
    pub bundle_name: String,
    pub bundle_version: String,
    pub format_version: String,
    pub payload_type: String,
    pub size_bytes: u64,
    pub checksum: String,
    pub created_at: String,
    pub file_path: String,
}
