using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Version compatibility checker
/// </summary>
public class VersionCompatibilityChecker : IVersionCompatibilityChecker
{
    /// <inheritdoc/>
    public bool IsCompatible(ExtensionMetadata extension, string hostVersion)
    {
        if (string.IsNullOrWhiteSpace(hostVersion))
        {
            return true; // No version constraint
        }

        if (!Version.TryParse(hostVersion, out var host))
        {
            return false; // Invalid host version
        }

        // Check minimum version
        if (!string.IsNullOrWhiteSpace(extension.MinHostVersion))
        {
            if (!Version.TryParse(extension.MinHostVersion, out var minVersion))
            {
                return false;
            }

            if (host < minVersion)
            {
                return false;
            }
        }

        // Check maximum version
        if (!string.IsNullOrWhiteSpace(extension.MaxHostVersion))
        {
            if (!Version.TryParse(extension.MaxHostVersion, out var maxVersion))
            {
                return false;
            }

            if (host > maxVersion)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public ExtensionMetadata? FindLatestCompatibleVersion(List<ExtensionMetadata> extensions, string hostVersion)
    {
        var compatible = extensions
            .Where(e => IsCompatible(e, hostVersion))
            .OrderByDescending(e => Version.TryParse(e.Version, out var v) ? v : new Version(0, 0))
            .FirstOrDefault();

        return compatible;
    }
}
