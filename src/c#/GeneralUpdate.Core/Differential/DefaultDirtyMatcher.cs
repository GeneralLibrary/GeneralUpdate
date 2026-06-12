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

        var findFile = patchFiles.FirstOrDefault(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f.Name);
            return name.Equals(oldFile.Name, System.StringComparison.OrdinalIgnoreCase);
        });

        if (findFile != null &&
            Path.GetExtension(findFile.FullName).Equals(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
            return findFile;

        return null;
    }
}
