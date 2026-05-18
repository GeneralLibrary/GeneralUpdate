using System.Runtime.InteropServices;

namespace Velaris.Sdk.Interop;

/// <summary>
/// P/Invoke declarations mapping to the Rust vela-ffi cdylib exports.
/// All Rust handles are opaque <see cref="IntPtr"/> values wrapped by
/// <see cref="SafeHandles.VelaSafeHandle"/> derivatives.
/// </summary>
internal static partial class NativeMethods
{
    private const string DllName = "vela_ffi";

    // ─── error buffer ──────────────────────────────────────────

    /// <summary>
    /// Get the last Rust error message. Returns null if no error.
    /// Caller must free with Marshal.FreeHGlobal.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr vela_last_error();

    /// <summary>Clear the Rust error buffer.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vela_clear_error();

    // ─── engine lifecycle ──────────────────────────────────────

    /// <summary>Initialize the Vela engine. Returns handle or IntPtr.Zero.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr vela_init();

    /// <summary>Shut down the engine. Returns 0 on success.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int vela_shutdown(IntPtr handle);

    // ─── FlashPack operations ──────────────────────────────────

    /// <summary>Open a FlashPack file. Returns handle or IntPtr.Zero.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi, BestFitMapping = false)]
    internal static extern IntPtr vela_fpk_open(string path);

    /// <summary>Close a FlashPack handle. Returns 0 on success.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int vela_fpk_close(IntPtr handle);
}
