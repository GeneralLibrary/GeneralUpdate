using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Defines the complete execution strategy for the Clean (patch-generation) phase.
    /// Implement this interface to fully control how source and target directories are
    /// compared and how the patch output is produced.
    /// </summary>
    public interface ICleanStrategy
    {
        /// <summary>
        /// Executes the Clean phase: compares <paramref name="sourcePath"/> with
        /// <paramref name="targetPath"/> and writes the resulting patch artifacts to
        /// <paramref name="patchPath"/>.
        /// </summary>
        Task ExecuteAsync(string sourcePath, string targetPath, string patchPath);
    }
}
