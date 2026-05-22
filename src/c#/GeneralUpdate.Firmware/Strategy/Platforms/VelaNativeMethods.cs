using System;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Firmware.Strategy.Platforms
{
    /// <summary>
    /// P/Invoke declarations for the vela native firmware library (vela-ffi).
    /// Maps all C-ABI exports from the vela-ffi Rust crate to C#.
    /// 
    /// <para>
    /// The native library is expected at:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Linux: <c>libvela_ffi.so</c></description></item>
    ///   <item><description>Windows: <c>vela_ffi.dll</c></description></item>
    /// </list>
    /// </summary>
    internal static class VelaNativeMethods
    {
        private const string DllName = "vela_ffi";

        /// <summary>
        /// Gets whether the vela native library is available on the current system.
        /// </summary>
        public static bool IsAvailable { get; }

        static VelaNativeMethods()
        {
            try
            {
                // Probe by calling vela_clear_error (a safe, no-op function)
                vela_clear_error();
                IsAvailable = true;
            }
            catch (DllNotFoundException)
            {
                IsAvailable = false;
            }
            catch (EntryPointNotFoundException)
            {
                IsAvailable = false;
            }
        }

        // ── Error handling ────────────────────────────────────────

        /// <summary>
        /// Gets the last error message from the vela engine.
        /// Returns null if no error has been recorded.
        /// Caller is NOT responsible for freeing the returned pointer
        /// (the native library owns the error buffer).
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr vela_last_error();

        /// <summary>
        /// Frees a string previously returned by a vela FFI function.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void vela_free_string(IntPtr ptr);

        /// <summary>
        /// Clears the last error.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void vela_clear_error();

        // ── Progress callback ─────────────────────────────────────

        /// <summary>
        /// Delegate for progress callback matching the native CProgressCallback signature.
        /// </summary>
        /// <param name="bytesWritten">Cumulative bytes written.</param>
        /// <param name="totalBytes">Total expected bytes (may be 0).</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ProgressCallbackDelegate(ulong bytesWritten, ulong totalBytes);

        /// <summary>
        /// Registers a C-compatible progress callback. Pass null to clear.
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_set_progress_callback(
            [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallbackDelegate callback);

        // ── Engine lifecycle ──────────────────────────────────────

        /// <summary>
        /// Initializes the vela flash engine with optional JSON config.
        /// Returns a non-null FlashHandle pointer on success, or IntPtr.Zero on failure.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr vela_flash_init(
            [MarshalAs(UnmanagedType.LPStr)] string configJson);

        /// <summary>
        /// Shuts down the vela flash engine and releases all resources.
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_shutdown(IntPtr handle);

        // ── Device validation ─────────────────────────────────────

        /// <summary>
        /// Validates that a block device exists and is writable.
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_validate_device(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string devicePath);

        // ── Backup ────────────────────────────────────────────────

        /// <summary>
        /// Reads the current firmware from a block device into a backup file.
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_backup_read(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string devicePath,
            [MarshalAs(UnmanagedType.LPStr)] string backupPath,
            [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallbackDelegate callback);

        // ── Flash: FPK install ────────────────────────────────────

        /// <summary>
        /// Installs a .fpk firmware bundle onto a block device.
        /// Returns the number of bytes written on success, or 0 on failure.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong vela_flash_write_fpk(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string fpkPath,
            [MarshalAs(UnmanagedType.LPStr)] string devicePath);

        // ── Flash: raw binary write ───────────────────────────────

        /// <summary>
        /// Writes a raw firmware binary to a block device.
        /// Returns the number of bytes written on success, or 0 on failure.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong vela_flash_write_raw(
            IntPtr handle,
            byte[] data,
            UIntPtr dataLen,
            [MarshalAs(UnmanagedType.LPStr)] string devicePath);

        // ── A/B Slot management ───────────────────────────────────

        /// <summary>
        /// Gets the currently active slot label.
        /// Caller must free the returned string with vela_free_string.
        /// Returns IntPtr.Zero if the handle is null.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr vela_flash_get_active_slot(IntPtr handle);

        /// <summary>
        /// Switches to the alternate slot after a successful flash.
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_switch_slot(IntPtr handle);

        /// <summary>
        /// Marks the current slot as healthy (good).
        /// Returns 0 on success.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int vela_flash_mark_good(IntPtr handle);

        // ── Helpers ───────────────────────────────────────────────

        /// <summary>
        /// Reads the last error message from the vela engine as a managed string.
        /// Returns null if no error.
        /// </summary>
        internal static string GetLastError()
        {
            IntPtr ptr = vela_last_error();
            if (ptr == IntPtr.Zero) return null;
            string msg = Marshal.PtrToStringAnsi(ptr);
            vela_free_string(ptr);
            return msg;
        }

        /// <summary>
        /// Reads the active slot label as a managed string.
        /// Returns null on failure.
        /// </summary>
        internal static string GetActiveSlot(IntPtr handle)
        {
            IntPtr ptr = vela_flash_get_active_slot(handle);
            if (ptr == IntPtr.Zero) return null;
            string slot = Marshal.PtrToStringAnsi(ptr);
            vela_free_string(ptr);
            return slot;
        }
    }
}
