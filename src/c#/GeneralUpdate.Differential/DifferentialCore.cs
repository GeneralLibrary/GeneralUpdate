using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace GeneralUpdate.Differential
{
    /// <summary>
    /// Entry point for differential update operations.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Differ.StreamingHdiffDiffer"/> with Deflate compression
    /// for smaller patches and faster application than the legacy BSDIFF algorithm.
    ///
    /// Pass a custom <see cref="IBinaryDiffer"/> to change the diff algorithm,
    /// or a custom <see cref="ICleanStrategy"/> / <see cref="IDirtyStrategy"/>
    /// for full control over execution flow.
    ///
    /// Legacy BSDIFF patches remain readable via <see cref="Binary.BinaryHandler"/>.
    /// </remarks>
    public static class DifferentialCore
    {
        /// <summary>
        /// Generates a binary patch from <paramref name="sourcePath"/> (old version)
        /// to <paramref name="targetPath"/> (new version), writing to <paramref name="patchPath"/>.
        /// </summary>
        /// <param name="binaryDiffer">
        /// The binary differ to use. Defaults to <see cref="Differ.StreamingHdiffDiffer"/>.
        /// When <paramref name="strategy"/> is non-null, the strategy owns the differ
        /// and this parameter is ignored.
        /// </param>
        /// <param name="strategy">
        /// Full execution strategy override. Defaults to <see cref="DefaultCleanStrategy"/>.
        /// </param>
        public static Task Clean(
            string sourcePath,
            string targetPath,
            string patchPath,
            IBinaryDiffer? binaryDiffer = null,
            ICleanStrategy? strategy = null)
        {
            var usedStrategy = strategy ?? new DefaultCleanStrategy(
                matcher: null,
                binaryDiffer: binaryDiffer);
            return usedStrategy.ExecuteAsync(sourcePath, targetPath, patchPath);
        }

        // Backward-compatible overload: 4th positional argument is a strategy.
        public static Task Clean(string sourcePath, string targetPath, string patchPath, ICleanStrategy? strategy)
            => Clean(sourcePath, targetPath, patchPath, strategy: strategy);

        /// <summary>
        /// Applies a binary patch from <paramref name="patchPath"/> to <paramref name="appPath"/>.
        /// </summary>
        /// <param name="binaryDiffer">
        /// The binary differ to use. Defaults to <see cref="Differ.StreamingHdiffDiffer"/>.
        /// When <paramref name="strategy"/> is non-null, the strategy owns the differ
        /// and this parameter is ignored.
        /// </param>
        /// <param name="strategy">
        /// Full execution strategy override. Defaults to <see cref="DefaultDirtyStrategy"/>.
        /// </param>
        public static Task Dirty(
            string appPath,
            string patchPath,
            IBinaryDiffer? binaryDiffer = null,
            IDirtyStrategy? strategy = null)
        {
            var usedStrategy = strategy ?? new DefaultDirtyStrategy(
                matcher: null,
                binaryDiffer: binaryDiffer);
            return usedStrategy.ExecuteAsync(appPath, patchPath);
        }

        // Backward-compatible overload: 3rd positional argument is a strategy.
        public static Task Dirty(string appPath, string patchPath, IDirtyStrategy? strategy)
            => Dirty(appPath, patchPath, strategy: strategy);
    }
}
