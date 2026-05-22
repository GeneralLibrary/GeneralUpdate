//! FFI layer: exports Vela Core functionality via C ABI for C# consumption.
//! This is the ONLY place where `unsafe` is permitted in the Vela workspace.

use std::sync::Mutex;
use tracing::{error, info, trace};

// Global last-error slot (thread-safe).
static LAST_ERROR: Mutex<Option<String>> = Mutex::new(None);

fn set_last_error(e: impl std::fmt::Display) {
    if let Ok(mut guard) = LAST_ERROR.lock() {
        *guard = Some(e.to_string());
    }
}

/// Opaque handle types for FFI.
pub struct FpkHandle {
    // inner: vela_flashpack::FlashPackReader,
}
pub struct EngineHandle {
    // inner: vela_lifecycle::LifecycleEngine,
}

/// Get the last error message. Caller must free the returned string.
/// Returns null if no error recorded.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_last_error() -> *mut std::os::raw::c_char {
    let err = LAST_ERROR.lock().ok().and_then(|g| g.clone());
    match err {
        Some(msg) => {
            let c_str = std::ffi::CString::new(msg).unwrap_or_default();
            c_str.into_raw()
        }
        None => std::ptr::null_mut(),
    }
}

/// Clear the last error.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_clear_error() {
    if let Ok(mut guard) = LAST_ERROR.lock() {
        *guard = None;
    }
}

/// Initialize the Vela Core engine. Returns engine handle or null on error.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_init() -> *mut EngineHandle {
    trace!("FFI: vela_init called");
    info!("Vela Core engine initialized");
    Box::into_raw(Box::new(EngineHandle {}))
}

/// Shut down and release the engine handle.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_shutdown(handle: *mut EngineHandle) -> i32 {
    if handle.is_null() {
        trace!("FFI: vela_shutdown called with null handle");
        return 1;
    }
    let _dropped = unsafe { Box::from_raw(handle) };
    info!("Vela Core engine shut down");
    0
}

/// Open a FlashPack file. Returns handle or null on error.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_fpk_open(path: *const std::os::raw::c_char) -> *mut FpkHandle {
    let path_str = match unsafe { std::ffi::CStr::from_ptr(path) }.to_str() {
        Ok(s) => s,
        Err(e) => {
            error!(error = %e, "vela_fpk_open: invalid UTF-8 path");
            return std::ptr::null_mut();
        }
    };
    trace!(path = %path_str, "FFI: vela_fpk_open called");
    // TODO: delegate to vela_flashpack::FlashPackReader::open
    set_last_error("vela_fpk_open: not yet implemented");
    std::ptr::null_mut()
}

/// Close a FlashPack handle and release resources.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vela_fpk_close(handle: *mut FpkHandle) -> i32 {
    if handle.is_null() {
        return 1;
    }
    let _dropped = unsafe { Box::from_raw(handle) };
    trace!("FFI: vela_fpk_close — handle released");
    0
}
