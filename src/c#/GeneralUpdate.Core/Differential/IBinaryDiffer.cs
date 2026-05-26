using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Pluggable file-level binary patch-application algorithm.
/// Implement this to customize how individual files are patched (BSDIFF, HDiffPatch, etc.).
///
/// For full directory-level control, inject <see cref="IDirtyStrategy"/> instead.
/// </summary>
public interface IBinaryDiffer
{
    /// <summary>Applies a binary patch: oldFile + patchFile → newFile.</summary>
    Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath,
        CancellationToken cancellationToken = default);
}
