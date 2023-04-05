using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Binary
{
    public interface IBinary
    {
        /// <summary>
        /// Sort out the patch .
        /// </summary>
        /// <param name="oldBytes">The file path of the previous version .</param>
        /// <param name="newBytes">Current version file path .</param>
        /// <param name="patchPath">Path to generate patch file .</param>
        /// <returns>future results .</returns>
        Task Clean(string oldfilePath, string newfilePath, string patchPath);

        /// <summary>
        /// Restore the patch.
        /// </summary>
        /// <param name="oldBytes">The file path of the previous version .</param>
        /// <param name="newBytes">Current version file path .</param>
        /// <param name="patchPath">Path to generate patch file .</param>
        /// <returns>future results .</returns>
        Task Dirty(string oldfilePath, string newfilePath, string patchPath);
    }
}