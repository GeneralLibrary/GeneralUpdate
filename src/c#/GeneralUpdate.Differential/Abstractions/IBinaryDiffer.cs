using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Abstractions;

/// <summary>
/// Pluggable file-level binary patch algorithm.
/// Implement this to customize how individual files are patched (BSDIFF, HDiffPatch, etc.).
/// </summary>
public interface IBinaryDiffer
{
    /// <summary>Applies a binary patch: oldFile + patchFile → newFile.</summary>
    Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a patch: oldFile vs newFile → patchFile.</summary>
    Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath,
        CancellationToken cancellationToken = default);
}
