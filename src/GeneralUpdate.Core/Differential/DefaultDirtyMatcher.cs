using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Default implementation of <see cref="IDirtyMatcher"/>: uses smart file name matching rules
/// to find the corresponding differential patch for each application file within the
/// patch file collection.
/// </summary>
/// <remarks>
/// <para>
/// Matching rules:
/// <list type="bullet">
///   <item><description>Patch files must have a <c>.patch</c> extension (case-insensitive).</description></item>
///   <item><description>The base name (without extension) of the patch file must match the application file name.</description></item>
///   <item><description>Supports nested <c>.patch</c> suffixes: e.g., <c>example.dll.patch</c>
///   strips <c>.patch</c> to yield <c>example.dll</c>, which is then compared to the
///   application file name.</description></item>
///   <item><description>All string comparisons are case-insensitive.</description></item>
/// </list>
/// </para>
/// <para>
/// This implementation corresponds to the "Dirty" (patch application) stage,
/// which is the counterpart to the differential generation stage in <see cref="DefaultCleanMatcher"/>.
/// </para>
/// </remarks>
public class DefaultDirtyMatcher : IDirtyMatcher
{
    private const string PatchFormat = ".patch";

    /// <inheritdoc/>
    public FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles)
    {
        if (oldFile is null) throw new ArgumentNullException(nameof(oldFile));
        if (patchFiles is null) throw new ArgumentNullException(nameof(patchFiles));

        var candidates = patchFiles.Where(f =>
        {
            if (!Path.GetExtension(f.FullName).Equals(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
                return false;
            var name = Path.GetFileNameWithoutExtension(f.Name);
            return name.Equals(oldFile.Name, System.StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Same file name exists in multiple patch directories: prefer the candidate whose
        // directory structure aligns most closely with the application file's directory,
        // so identical names in different subdirectories receive their own patch instead
        // of whichever candidate happened to enumerate first.
        return candidates
            .OrderByDescending(c => CommonSuffixSegments(oldFile.DirectoryName, c.DirectoryName))
            .First();
    }

    /// <summary>
    /// Counts the trailing directory segments shared by the two paths (case-insensitive).
    /// </summary>
    private static int CommonSuffixSegments(string? dirA, string? dirB)
    {
        if (string.IsNullOrEmpty(dirA) || string.IsNullOrEmpty(dirB)) return 0;
        var a = dirA.TrimEnd('\\', '/').Split('\\', '/');
        var b = dirB.TrimEnd('\\', '/').Split('\\', '/');
        int i = a.Length - 1, j = b.Length - 1, shared = 0;
        while (i >= 0 && j >= 0 &&
               string.Equals(a[i], b[j], System.StringComparison.OrdinalIgnoreCase))
        {
            shared++;
            i--;
            j--;
        }
        return shared;
    }
}
