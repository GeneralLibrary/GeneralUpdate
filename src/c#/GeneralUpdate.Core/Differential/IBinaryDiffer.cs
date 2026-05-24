using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Differential
{
    /// <summary>
    /// Defines a pluggable binary differential algorithm (diff generation and patch application).
    /// Implementations may use different strategies: BSDIFF, HDiffPatch-style, VCDIFF, etc.
    /// </summary>
    /// <remarks>
    /// This interface lives in Core so that Pipeline middleware can depend on it
    /// without creating a circular dependency on the GeneralUpdate.Differential assembly.
    ///
    /// Concrete implementations (StreamingHdiffDiffer, BSDIFF, etc.) live in
    /// GeneralUpdate.Differential and are injected via Bootstrap.BinaryDiffer&lt;T&gt;().
    /// </remarks>
    public interface IBinaryDiffer
    {
        /// <summary>
        /// Generates a binary patch from <paramref name="oldFilePath"/> to <paramref name="newFilePath"/>,
        /// writing the result to <paramref name="patchFilePath"/>.
        /// </summary>
        Task CleanAsync(
            string oldFilePath,
            string newFilePath,
            string patchFilePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a binary patch to <paramref name="oldFilePath"/>, producing
        /// <paramref name="newFilePath"/> using the patch at <paramref name="patchFilePath"/>.
        /// </summary>
        Task DirtyAsync(
            string oldFilePath,
            string newFilePath,
            string patchFilePath,
            CancellationToken cancellationToken = default);
    }
}
