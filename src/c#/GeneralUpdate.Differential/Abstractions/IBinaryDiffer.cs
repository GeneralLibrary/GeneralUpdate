using System.Threading;
using System.Threading.Tasks;
using CoreBinaryDiffer = GeneralUpdate.Core.Differential.IBinaryDiffer;

namespace GeneralUpdate.Differential.Abstractions;

/// <summary>
/// Binary differential algorithm with both patch generation and application.
/// Extends <see cref="CoreBinaryDiffer"/> (DirtyAsync) with CleanAsync.
/// </summary>
public interface IBinaryDiffer : CoreBinaryDiffer
{
    /// <summary>Generates a patch: oldFile vs newFile → patchFile.</summary>
    Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath,
        CancellationToken cancellationToken = default);
}
