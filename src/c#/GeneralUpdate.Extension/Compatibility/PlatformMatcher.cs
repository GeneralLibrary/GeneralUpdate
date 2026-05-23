using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Platform matching utility. Delegates OS detection to <see cref="IPlatformServices"/> for testability.
/// </summary>
public class PlatformMatcher : IPlatformMatcher
{
    private readonly IPlatformServices _platformServices;

    /// <summary>
    /// Initialize with platform services (default: production runtime-based detection).
    /// </summary>
    public PlatformMatcher(IPlatformServices? platformServices = null)
    {
        _platformServices = platformServices ?? new RuntimePlatformServices();
    }

    /// <inheritdoc/>
    public TargetPlatform GetCurrentPlatform()
    {
        return _platformServices.GetCurrentPlatform();
    }

    /// <inheritdoc/>
    public bool IsCurrentPlatformSupported(ExtensionMetadata extension)
    {
        var currentPlatform = GetCurrentPlatform();
        return (extension.SupportedPlatforms & currentPlatform) != 0;
    }

    /// <inheritdoc/>
    public bool IsPlatformSupported(ExtensionMetadata extension, TargetPlatform platform)
    {
        return (extension.SupportedPlatforms & platform) != 0;
    }
}
