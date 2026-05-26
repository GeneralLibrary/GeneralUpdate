using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Default implementation of <see cref="IDirtyMatcher"/>:
/// a patch file matches an application file when the patch file's name
/// (without the <c>.patch</c> extension, case-insensitive) equals the application file's name,
/// and the patch file carries the <c>.patch</c> extension.
/// </summary>
public class DefaultDirtyMatcher : IDirtyMatcher
{
    private const string PatchFormat = ".patch";

    /// <inheritdoc/>
    public FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles)
    {
        var findFile = patchFiles.FirstOrDefault(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f.Name);
            if (name.EndsWith(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - PatchFormat.Length);
            return name.Equals(oldFile.Name, System.StringComparison.OrdinalIgnoreCase);
        });

        if (findFile != null &&
            Path.GetExtension(findFile.FullName).Equals(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
            return findFile;

        return null;
    }
}
