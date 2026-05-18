using System.Runtime.InteropServices;

namespace Velaris.Sdk;

/// <summary>
/// Represents an error originating from the native Vela Rust engine.
/// The message is retrieved from the Rust FFI <c>vela_last_error</c> buffer.
/// </summary>
public sealed class VelaException : Exception
{
    /// <summary>
    /// Create a VelaException by fetching the last error from the native engine.
    /// Clears the error buffer after reading.
    /// </summary>
    public static VelaException FromLastError(string operation)
    {
        var ptr = Interop.NativeMethods.vela_last_error();
        var message = ptr != IntPtr.Zero
            ? Marshal.PtrToStringUTF8(ptr) ?? "unknown error"
            : "unknown error";

        // Free the C string allocated by Rust
        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);

        // Clear the Rust error buffer
        Interop.NativeMethods.vela_clear_error();

        return new VelaException($"{operation}: {message}");
    }

    /// <summary>
    /// Throw if the handle is invalid (native call returned null/zero).
    /// Fetches the last Rust error as the exception message.
    /// </summary>
    internal static void ThrowIfInvalid(IntPtr handle, string operation)
    {
        if (handle == IntPtr.Zero)
            throw FromLastError(operation);
    }

    public VelaException(string message) : base(message) { }
    public VelaException(string message, Exception inner) : base(message, inner) { }
}
