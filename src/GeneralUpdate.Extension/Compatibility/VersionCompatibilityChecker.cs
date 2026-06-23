using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Version compatibility checker — uses SemVer 2.0 for all comparisons.
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

        if (!Semver.TryParse(hostVersion, out var host))
        {
            return false; // Invalid host version
        }

        // Check minimum version
        if (!string.IsNullOrWhiteSpace(extension.MinHostVersion))
        {
            if (!Semver.TryParse(extension.MinHostVersion, out var minVersion))
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
            if (!Semver.TryParse(extension.MaxHostVersion, out var maxVersion))
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
        if (extensions == null || extensions.Count == 0)
            return null;

        // Parse valid versions only; unparseable version strings are treated as "unknown"
        // and sorted AFTER all valid versions (so they don't accidentally become "latest")
        var parsed = extensions
            .Select(e => new
            {
                Extension = e,
                IsCompatible = IsCompatible(e, hostVersion),
                HasValidVersion = Semver.TryParse(e.Version, out var v),
                ParsedVersion = v
            })
            .ToList();

        // Filter to compatible only, then sort: valid versions descending, then unknown versions.
        // Use Semver.Compare for proper SemVer 2.0 ordering with prerelease support.
        var best = parsed
            .Where(x => x.IsCompatible)
            .OrderBy(x => x.HasValidVersion ? 0 : 1)          // valid versions first
            .ThenByDescending(x => x.HasValidVersion ? x.ParsedVersion.ToString() : null,
                SemverComparer.Instance)                      // highest version first
            .FirstOrDefault();

        return best?.Extension;
    }
}
