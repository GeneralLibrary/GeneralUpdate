using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Interface for platform matching operations
/// </summary>
public interface IPlatformMatcher
{
    /// <summary>
    /// Get current platform
    /// </summary>
    /// <returns>Current target platform</returns>
    TargetPlatform GetCurrentPlatform();

    /// <summary>
    /// Check if extension supports current platform
    /// </summary>
    /// <param name="extension">Extension metadata</param>
    /// <returns>True if platform is supported</returns>
    bool IsCurrentPlatformSupported(ExtensionMetadata extension);

    /// <summary>
    /// Check if extension supports specified platform
    /// </summary>
    /// <param name="extension">Extension metadata</param>
    /// <param name="platform">Target platform to check</param>
    /// <returns>True if platform is supported</returns>
    bool IsPlatformSupported(ExtensionMetadata extension, TargetPlatform platform);
}
