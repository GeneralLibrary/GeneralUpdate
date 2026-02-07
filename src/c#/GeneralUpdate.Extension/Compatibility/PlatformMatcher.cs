using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Platform matching utility
/// </summary>
public class PlatformMatcher : IPlatformMatcher
{
    /// <inheritdoc/>
    public TargetPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TargetPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return TargetPlatform.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return TargetPlatform.MacOS;
        }

        return TargetPlatform.None;
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
