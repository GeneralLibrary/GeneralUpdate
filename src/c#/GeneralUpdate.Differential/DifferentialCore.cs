using System.Threading.Tasks;
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
    /// </summary>
    public static class DifferentialCore
    {
        /// <summary>
        /// Compares <paramref name="sourcePath"/> with <paramref name="targetPath"/> and
        /// writes patch artifacts to <paramref name="patchPath"/>.
        /// </summary>
        /// <param name="strategy">
        /// Optional strategy that fully controls the execution.
        /// Defaults to <see cref="DefaultCleanStrategy"/> when <c>null</c>.
        /// </param>
        public static Task Clean(string sourcePath, string targetPath, string patchPath, ICleanStrategy? strategy = null)
            => (strategy ?? new DefaultCleanStrategy()).ExecuteAsync(sourcePath, targetPath, patchPath);

        /// <summary>
        /// Applies patches from <paramref name="patchPath"/> to the application files in
        /// <paramref name="appPath"/>.
        /// </summary>
        /// <param name="strategy">
        /// Optional strategy that fully controls the execution.
        /// Defaults to <see cref="DefaultDirtyStrategy"/> when <c>null</c>.
        /// </param>
        public static Task Dirty(string appPath, string patchPath, IDirtyStrategy? strategy = null)
            => (strategy ?? new DefaultDirtyStrategy()).ExecuteAsync(appPath, patchPath);
    }
}