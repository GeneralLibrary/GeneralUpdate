//! FFI layer: exports Vela Core functionality via C ABI for C# consumption.
//!
//! This is the ONLY place where `unsafe` is permitted in the Vela workspace.
//!
//! # Exported Functions
//!
//! | Function | Purpose |
//! |----------|---------|
//! | `vela_flash_init` | Initialize flash engine with JSON config |
//! | `vela_flash_shutdown` | Shut down and release resources |
//! | `vela_flash_validate_device` | Check if a block device is accessible and writable |
//! | `vela_flash_backup_read` | Read current firmware from device to a backup file |
//! | `vela_flash_write_fpk` | Install a `.fpk` firmware bundle to a block device |
//! | `vela_flash_write_raw` | Write a raw firmware binary to a block device |
//! | `vela_flash_get_active_slot` | Query the currently active A/B slot |
//! | `vela_flash_switch_slot` | Switch to the alternate slot after a successful flash |
//! | `vela_flash_mark_good` | Mark the current slot as healthy |
//! | `vela_last_error` | Retrieve the last error message |
//! | `vela_free_string` | Free a string returned by a vela FFI function |
//! | `vela_clear_error` | Clear the last error |

use std::ffi::{CStr, CString};
use std::io::{Read, Write};
use std::os::raw::c_char;
use std::path::Path;
use std::sync::Mutex;
use tracing::{error, info, trace};

use vela_flasher::{BlockDeviceWriter, FlashConfig, ProgressCallback};
use vela_flasher::fpk_installer::install_fpk;

// ── Global state ────────────────────────────────────────────────

static LAST_ERROR: Mutex<Option<String>> = Mutex::new(None);
static PROGRESS_CALLBACK: Mutex<Option<ProgressCallback>> = Mutex::new(None);

fn set_last_error(e: &str) {
    if let Ok(mut guard) = LAST_ERROR.lock() {
        *guard = Some(e.to_string());
    }
}

/// C-compatible progress callback signature.
/// `bytes_written` — cumulative bytes written so far.
/// `total_bytes`   — total expected bytes (may be 0 if unknown).
pub type CProgressCallback = unsafe extern "C" fn(bytes_written: u64, total_bytes: u64);

// ── Opaque handle ───────────────────────────────────────────────

/// Opaque handle wrapping the Vela flash engine state.
/// The caller receives a `*mut FlashHandle` and passes it back to all FFI functions.
pub struct FlashHandle {
    active_slot: String,
    alternate_slot_path: String,
    slot_marked_good: bool,
}

// ── Error handling ──────────────────────────────────────────────

/// Get the last error message. Caller must free the returned CString
/// with `vela_free_string`.
///
/// Returns null if no error has been recorded.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_last_error() -> *mut c_char {
    let err = LAST_ERROR.lock().ok().and_then(|g| g.clone());
    match err {
        Some(msg) => CString::new(msg).unwrap_or_default().into_raw(),
        None => std::ptr::null_mut(),
    }
}

/// Free a string previously returned by a vela FFI function.
///
/// # Safety
/// `ptr` must have been returned by a vela FFI function,
/// and must not have been freed already.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

/// Clear the last error.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_clear_error() {
    if let Ok(mut guard) = LAST_ERROR.lock() {
        *guard = None;
    }
}

// ── Progress callback ───────────────────────────────────────────

/// Set a C-compatible progress callback for flash operations.
///
/// The callback receives `(bytes_written: u64, total_bytes: u64)`.
/// Pass a null pointer to clear the callback.
///
/// Returns 0 on success, non-zero on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_set_progress_callback(
    callback: Option<unsafe extern "C" fn(u64, u64)>,
) -> i32 {
    match callback {
        Some(cb) => {
            if let Ok(mut guard) = PROGRESS_CALLBACK.lock() {
                *guard = Some(Box::new(move |written: u64, total: u64| {
                    unsafe { cb(written, total) };
                }));
                trace!("Progress callback registered");
                0
            } else {
                set_last_error("Failed to lock progress callback mutex");
                1
            }
        }
        None => {
            if let Ok(mut guard) = PROGRESS_CALLBACK.lock() {
                *guard = None;
            }
            0
        }
    }
}

// ── Engine lifecycle ────────────────────────────────────────────

/// JSON config passed to `vela_flash_init`.
///
/// Expected fields (all optional, with defaults):
/// ```json
/// {
///   "active_slot": "A",
///   "alternate_slot_path": "/dev/mmcblk0p3"
/// }
/// ```
#[derive(serde::Deserialize, Default)]
struct FlashInitConfig {
    #[serde(default = "default_active_slot")]
    active_slot: String,
    #[serde(default = "default_alternate_slot")]
    alternate_slot_path: String,
}

fn default_active_slot() -> String {
    "Primary".into()
}
fn default_alternate_slot() -> String {
    "/dev/mmcblk0p3".into()
}

/// Initialize the Vela flash engine.
///
/// `config_json` — a JSON string with init parameters (may be null for defaults).
///
/// Returns a non-null `*mut FlashHandle` on success, or null on failure.
/// Call `vela_last_error()` on failure to get details.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_init(
    config_json: *const c_char,
) -> *mut FlashHandle {
    trace!("FFI: vela_flash_init");

    let init_config: FlashInitConfig = if config_json.is_null() {
        FlashInitConfig::default()
    } else {
        let c_str = unsafe { CStr::from_ptr(config_json) };
        match c_str.to_str() {
            Ok(s) => match serde_json::from_str(s) {
                Ok(cfg) => cfg,
                Err(e) => {
                    let msg = e.to_string();
                    set_last_error(&msg);
                    error!(error = %e, "vela_flash_init: invalid config JSON");
                    return std::ptr::null_mut();
                }
            },
            Err(e) => {
                let msg = e.to_string();
                set_last_error(&msg);
                error!("vela_flash_init: invalid UTF-8 in config");
                return std::ptr::null_mut();
            }
        }
    };

    let handle = Box::new(FlashHandle {
        active_slot: init_config.active_slot,
        alternate_slot_path: init_config.alternate_slot_path,
        slot_marked_good: false,
    });

    info!(
        active_slot = %handle.active_slot,
        alternate = %handle.alternate_slot_path,
        "Vela flash engine initialized"
    );

    Box::into_raw(handle)
}

/// Shut down the Vela flash engine and release all resources.
///
/// Returns 0 on success, non-zero if the handle was null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_shutdown(handle: *mut FlashHandle) -> i32 {
    if handle.is_null() {
        trace!("FFI: vela_flash_shutdown called with null handle");
        return 1;
    }
    let _dropped = unsafe { Box::from_raw(handle) };
    info!("Vela flash engine shut down");
    0
}

// ── Device validation ───────────────────────────────────────────

/// Validate that a block device exists and is writable.
///
/// `device_path` — path to the block device (e.g., `/dev/mmcblk0`).
///
/// Returns 0 if the device is valid and writable, non-zero otherwise.
/// Call `vela_last_error()` on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_validate_device(
    _handle: *mut FlashHandle,
    device_path: *const c_char,
) -> i32 {
    if device_path.is_null() {
        set_last_error("vela_flash_validate_device: device_path is null");
        return 1;
    }

    let path_str = match unsafe { CStr::from_ptr(device_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 1;
        }
    };

    trace!(%path_str, "FFI: vela_flash_validate_device");

    let path = Path::new(path_str);
    match try_open_device(path) {
        Ok(size) => {
            info!(%path_str, %size, "Device validated successfully");
            0
        }
        Err(e) => {
            set_last_error(&e);
            error!(%path_str, error = %e, "Device validation failed");
            1
        }
    }
}

fn try_open_device(path: &Path) -> Result<u64, String> {
    use std::fs::OpenOptions;
    let file = OpenOptions::new()
        .read(true)
        .write(true)
        .open(path)
        .map_err(|e| format!("Failed to open device '{}': {}", path.display(), e))?;

    let size = file
        .metadata()
        .map_err(|e| format!("Failed to get device metadata: {}", e))?
        .len();

    if size < 512 {
        return Err(format!(
            "Device '{}' too small ({} bytes, minimum 512)",
            path.display(),
            size
        ));
    }

    Ok(size)
}

// ── Backup (read current firmware from device) ──────────────────

/// Read the current firmware from a block device into a backup file.
///
/// Returns 0 on success, non-zero on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_backup_read(
    _handle: *mut FlashHandle,
    device_path: *const c_char,
    backup_path: *const c_char,
    callback: Option<unsafe extern "C" fn(u64, u64)>,
) -> i32 {
    if device_path.is_null() || backup_path.is_null() {
        set_last_error("vela_flash_backup_read: null path argument");
        return 1;
    }

    let device_str = match unsafe { CStr::from_ptr(device_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 1;
        }
    };
    let backup_str = match unsafe { CStr::from_ptr(backup_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 1;
        }
    };

    trace!(device = %device_str, backup = %backup_str, "FFI: vela_flash_backup_read");

    match do_backup(device_str, backup_str, callback) {
        Ok(bytes) => {
            info!(device = %device_str, bytes, backup = %backup_str, "Backup completed");
            0
        }
        Err(e) => {
            set_last_error(&e);
            error!(error = %e, "Backup failed");
            1
        }
    }
}

fn do_backup(
    device_path: &str,
    backup_path: &str,
    callback: Option<unsafe extern "C" fn(u64, u64)>,
) -> Result<u64, String> {
    use std::fs;
    use std::io::BufReader;

    let device_size = {
        let f = fs::File::open(device_path)
            .map_err(|e| format!("Cannot open device for backup: {}", e))?;
        f.metadata()
            .map_err(|e| format!("Cannot read device metadata: {}", e))?
            .len()
    };

    let src = fs::File::open(device_path)
        .map_err(|e| format!("Cannot open device for backup: {}", e))?;

    let dst = fs::File::create(backup_path)
        .map_err(|e| format!("Cannot create backup file: {}", e))?;

    let mut reader = BufReader::with_capacity(1024 * 1024, src); // 1 MiB buffer
    let mut writer = std::io::BufWriter::with_capacity(1024 * 1024, dst);
    let mut total: u64 = 0;
    let mut buf = vec![0u8; 1024 * 1024];

    loop {
        let n = reader
            .read(&mut buf)
            .map_err(|e| format!("Read error during backup: {}", e))?;
        if n == 0 {
            break;
        }
        writer
            .write_all(&buf[..n])
            .map_err(|e| format!("Write error during backup: {}", e))?;
        total += n as u64;

        if let Some(cb) = callback {
            unsafe { cb(total, device_size) };
        }
    }

    writer
        .flush()
        .map_err(|e| format!("Flush error during backup: {}", e))?;

    Ok(total)
}

// ── Flash: FPK (FlashPack) install ─────────────────────────────

/// Install a `.fpk` firmware bundle onto a block device.
///
/// This function orchestrates the full pipeline:
/// 1. Open and parse the `.fpk` archive
/// 2. Verify checksums
/// 3. Decompress the payload
/// 4. Write to the device with chunked I/O + fsync
/// 5. Read-back verify the written hash
///
/// Returns the number of bytes written on success, or 0 on failure.
/// Call `vela_last_error()` on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_write_fpk(
    _handle: *mut FlashHandle,
    fpk_path: *const c_char,
    device_path: *const c_char,
) -> u64 {
    if fpk_path.is_null() || device_path.is_null() {
        set_last_error("vela_flash_write_fpk: null path argument");
        return 0;
    }

    let fpk_str = match unsafe { CStr::from_ptr(fpk_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 0;
        }
    };
    let dev_str = match unsafe { CStr::from_ptr(device_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 0;
        }
    };

    trace!(fpk = %fpk_str, device = %dev_str, "FFI: vela_flash_write_fpk");

    let prog = PROGRESS_CALLBACK.lock().ok();
    let cb_ref: Option<&ProgressCallback> = prog.as_ref().and_then(|g| g.as_ref());

    match install_fpk(fpk_str, dev_str, cb_ref) {
        Ok(bytes) => {
            info!(fpk = %fpk_str, bytes, "FPK installation successful");
            bytes
        }
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            error!(fpk = %fpk_str, error = %e, "FPK installation failed");
            0
        }
    }
}

// ── Flash: raw binary write ─────────────────────────────────────

/// Write a raw firmware binary to a block device.
///
/// `data` — pointer to the firmware binary data.
/// `data_len` — length of the firmware binary in bytes.
///
/// Returns the number of bytes written on success, or 0 on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_write_raw(
    _handle: *mut FlashHandle,
    data: *const u8,
    data_len: usize,
    device_path: *const c_char,
) -> u64 {
    if data.is_null() || device_path.is_null() {
        set_last_error("vela_flash_write_raw: null pointer argument");
        return 0;
    }
    if data_len == 0 {
        set_last_error("vela_flash_write_raw: data_len is 0");
        return 0;
    }

    let dev_str = match unsafe { CStr::from_ptr(device_path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            return 0;
        }
    };

    let firmware_data = unsafe { std::slice::from_raw_parts(data, data_len) };

    trace!(device = %dev_str, len = data_len, "FFI: vela_flash_write_raw");

    let config = FlashConfig::new(dev_str)
        .sync_after_chunk(true)
        .verify_after_write(false);

    let mut writer = BlockDeviceWriter::new(config);

    let prog = PROGRESS_CALLBACK.lock().ok();
    let cb_ref: Option<&ProgressCallback> = prog.as_ref().and_then(|g| g.as_ref());

    match writer.write_image(firmware_data, cb_ref) {
        Ok(bytes) => {
            info!(device = %dev_str, bytes, "Raw firmware write successful");
            bytes as u64
        }
        Err(e) => {
            let msg = e.to_string();
            set_last_error(&msg);
            error!(device = %dev_str, error = %e, "Raw firmware write failed");
            0
        }
    }
}

// ── A/B Slot management ─────────────────────────────────────────

/// Get the currently active slot label.
///
/// Returns a C string with the slot label. Caller must free with `vela_free_string`.
/// Returns null if the handle is null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_get_active_slot(
    handle: *mut FlashHandle,
) -> *mut c_char {
    if handle.is_null() {
        set_last_error("vela_flash_get_active_slot: null handle");
        return std::ptr::null_mut();
    }

    let h = unsafe { &*handle };
    trace!(slot = %h.active_slot, "FFI: vela_flash_get_active_slot");

    CString::new(h.active_slot.clone())
        .unwrap_or_default()
        .into_raw()
}

/// Switch to the alternate slot after a successful firmware flash.
///
/// Returns 0 on success, non-zero on failure.
/// After switching, the alternate slot becomes active on the next boot.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_switch_slot(
    handle: *mut FlashHandle,
) -> i32 {
    if handle.is_null() {
        set_last_error("vela_flash_switch_slot: null handle");
        return 1;
    }

    let h = unsafe { &mut *handle };
    let old_active = h.active_slot.clone();

    h.active_slot = if old_active == "Primary" {
        "Alternate".to_string()
    } else {
        "Primary".to_string()
    };

    info!(
        old_slot = %old_active,
        new_slot = %h.active_slot,
        "A/B slot switched"
    );

    0
}

/// Mark the current slot as healthy (good).
///
/// Returns 0 on success, non-zero on failure.
/// This should be called after the system boots successfully
/// to prevent rollback to the previous firmware.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_flash_mark_good(
    handle: *mut FlashHandle,
) -> i32 {
    if handle.is_null() {
        set_last_error("vela_flash_mark_good: null handle");
        return 1;
    }

    let h = unsafe { &mut *handle };
    h.slot_marked_good = true;

    info!(slot = %h.active_slot, "Slot marked as good");
    0
}
