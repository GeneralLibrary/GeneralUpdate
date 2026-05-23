using System.Runtime.InteropServices;
using GeneralUpdate.Extension.Common.Enums;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Production implementation of <see cref="IPlatformServices"/> using .NET RuntimeInformation.
/// </summary>
public class RuntimePlatformServices : IPlatformServices
{
    /// <inheritdoc/>
    public TargetPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TargetPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return TargetPlatform.Linux;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return TargetPlatform.MacOS;

        return TargetPlatform.None;
    }
}
