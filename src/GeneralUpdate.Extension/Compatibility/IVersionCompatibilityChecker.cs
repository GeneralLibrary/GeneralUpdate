using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Interface for version compatibility checking
/// </summary>
public interface IVersionCompatibilityChecker
{
    /// <summary>
    /// Check if extension is compatible with host version
    /// </summary>
    /// <param name="extension">Extension metadata</param>
    /// <param name="hostVersion">Host application version</param>
    /// <returns>True if compatible</returns>
    bool IsCompatible(ExtensionMetadata extension, string hostVersion);

    /// <summary>
    /// Find the latest compatible version from a list of extensions
    /// </summary>
    /// <param name="extensions">List of extension versions</param>
    /// <param name="hostVersion">Host application version</param>
    /// <returns>Latest compatible extension or null</returns>
    ExtensionMetadata? FindLatestCompatibleVersion(List<ExtensionMetadata> extensions, string hostVersion);
}
