using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Velaris.Sdk.SafeHandles;

/// <summary>
/// Base class for safe handles that wrap Rust opaque pointers.
/// Guarantees cleanup even if finalization occurs.
/// </summary>
internal abstract class VelaSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    protected VelaSafeHandle(bool ownsHandle)
        : base(ownsHandle)
    {
    }
}

/// <summary>
/// Safe handle wrapping a FlashPack reader (vela_fpk_* functions).
/// </summary>
internal sealed class FlashPackSafeHandle : VelaSafeHandle
{
    public FlashPackSafeHandle(IntPtr handle, bool ownsHandle = true)
        : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid) return true;
        return Interop.NativeMethods.vela_fpk_close(handle) == 0;
    }
}

/// <summary>
/// Safe handle wrapping the Vela lifecycle engine (vela_init/vela_shutdown).
/// </summary>
internal sealed class EngineSafeHandle : VelaSafeHandle
{
    public EngineSafeHandle(IntPtr handle, bool ownsHandle = true)
        : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid) return true;
        return Interop.NativeMethods.vela_shutdown(handle) == 0;
    }
}
