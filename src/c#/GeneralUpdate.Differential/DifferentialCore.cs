using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Binary;
using GeneralUpdate.Differential.Matchers;

namespace GeneralUpdate.Differential
{
    /// <summary>
    /// Entry point for differential update operations.
    /// <para>
    /// Both methods accept an optional strategy parameter that gives callers full
    /// control over the execution logic.  When no strategy is supplied, the built-in
    /// default behaviour is used unchanged.
    /// </para>
    /// <para>
    /// To customise only the file-matching step within the default flow, construct a
    /// <see cref="DefaultCleanStrategy"/> or <see cref="DefaultDirtyStrategy"/> with a
    /// custom <see cref="ICleanMatcher"/> or <see cref="IDirtyMatcher"/> respectively.
    /// </para>
    /// <para>
    /// Beginning with v2, you may also supply a custom <see cref="IBinaryDiffer"/>
    /// implementation to replace the BSDIFF algorithm for different trade-offs
    /// (speed, patch size, memory, streaming support).
    /// </para>
    /// </summary>
    public static class DifferentialCore
    {
        #region Clean (Patch Generation)

        /// <summary>
        /// Compares <paramref name="sourcePath"/> with <paramref name="targetPath"/> and
        /// writes patch artifacts to <paramref name="patchPath"/>.
        /// Uses the default BSDIFF binary differ and default clean strategy.
        /// </summary>
        /// <param name="strategy">
        /// Optional strategy that fully controls the execution.
        /// Defaults to <see cref="DefaultCleanStrategy"/> when <c>null</c>.
        /// </param>
        public static Task Clean(string sourcePath, string targetPath, string patchPath, ICleanStrategy? strategy = null)
            => (strategy ?? new DefaultCleanStrategy()).ExecuteAsync(sourcePath, targetPath, patchPath);

        /// <summary>
        /// Compares <paramref name="sourcePath"/> with <paramref name="targetPath"/> and
        /// writes patch artifacts to <paramref name="patchPath"/> using the specified binary differ.
        /// </summary>
        /// <param name="sourcePath">The source (old-version) directory.</param>
        /// <param name="targetPath">The target (new-version) directory.</param>
        /// <param name="patchPath">Output patch directory.</param>
        /// <param name="binaryDiffer">The binary differ algorithm to use.</param>
        /// <param name="strategy">Optional strategy override.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public static Task Clean(
            string sourcePath,
            string targetPath,
            string patchPath,
            IBinaryDiffer binaryDiffer,
            ICleanStrategy? strategy = null,
            CancellationToken cancellationToken = default)
        {
            var cleanMatcher = new DefaultCleanMatcher();
            var usedStrategy = strategy ?? new DefaultCleanStrategy(cleanMatcher, binaryDiffer);
            return usedStrategy.ExecuteAsync(sourcePath, targetPath, patchPath);
        }

        #endregion Clean (Patch Generation)

        #region Dirty (Patch Application)

        /// <summary>
        /// Applies patches from <paramref name="patchPath"/> to the application files in
        /// <paramref name="appPath"/>.
        /// Uses the default BSDIFF binary differ and default dirty strategy.
        /// </summary>
        /// <param name="strategy">
        /// Optional strategy that fully controls the execution.
        /// Defaults to <see cref="DefaultDirtyStrategy"/> when <c>null</c>.
        /// </param>
        public static Task Dirty(string appPath, string patchPath, IDirtyStrategy? strategy = null)
            => (strategy ?? new DefaultDirtyStrategy()).ExecuteAsync(appPath, patchPath);

        /// <summary>
        /// Applies patches from <paramref name="patchPath"/> to the application files in
        /// <paramref name="appPath"/> using the specified binary differ.
        /// </summary>
        /// <param name="appPath">The application directory to patch.</param>
        /// <param name="patchPath">The patch directory.</param>
        /// <param name="binaryDiffer">The binary differ algorithm to use.</param>
        /// <param name="strategy">Optional strategy override.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public static Task Dirty(
            string appPath,
            string patchPath,
            IBinaryDiffer binaryDiffer,
            IDirtyStrategy? strategy = null,
            CancellationToken cancellationToken = default)
        {
            var dirtyMatcher = new DefaultDirtyMatcher();
            var usedStrategy = strategy ?? new DefaultDirtyStrategy(dirtyMatcher, binaryDiffer);
            return usedStrategy.ExecuteAsync(appPath, patchPath);
        }

        #endregion Dirty (Patch Application)
    }
}
