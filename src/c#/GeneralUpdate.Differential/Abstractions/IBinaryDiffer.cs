using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// Defines a pluggable binary differential algorithm (diff generation and patch application).
    /// Implementations may use different strategies: BSDIFF, HDiffPatch-style, VCDIFF, etc.
    /// All methods are stateless 鈥?callers pass file paths and instances are safe for concurrent use.
    /// </summary>
    public interface IBinaryDiffer
    {
        /// <summary>
        /// Generates a binary patch from <paramref name="oldFilePath"/> to <paramref name="newFilePath"/>,
        /// writing the result to <paramref name="patchFilePath"/>.
        /// </summary>
        /// <param name="oldFilePath">Old version file path.</param>
        /// <param name="newFilePath">New version file path.</param>
        /// <param name="patchFilePath">Output patch file path.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task CleanAsync(
            string oldFilePath,
            string newFilePath,
            string patchFilePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a binary patch to <paramref name="oldFilePath"/>, producing
        /// <paramref name="newFilePath"/> using the patch at <paramref name="patchFilePath"/>.
        /// </summary>
        /// <param name="oldFilePath">Existing (old) file to patch.</param>
        /// <param name="newFilePath">Output (new) file path.</param>
        /// <param name="patchFilePath">Input patch file path.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task DirtyAsync(
            string oldFilePath,
            string newFilePath,
            string patchFilePath,
            CancellationToken cancellationToken = default);
    }
}
