using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Defines the complete execution strategy for the Dirty (patch-application) phase.
    /// Implement this interface to fully control how patch files are applied to the
    /// target application directory.
    /// </summary>
    public interface IDirtyStrategy
    {
        /// <summary>
        /// Executes the Dirty phase: applies patches from <paramref name="patchPath"/>
        /// to the application files in <paramref name="appPath"/>.
        /// </summary>
        Task ExecuteAsync(string appPath, string patchPath);
    }
}
