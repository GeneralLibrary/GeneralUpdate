using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Defines the matching logic used during the Dirty (patch-application) phase to
    /// find the patch file that corresponds to an existing application file.
    /// </summary>
    public interface IDirtyMatcher
    {
        /// <summary>
        /// Attempts to find the patch file for <paramref name="oldFile"/> from the
        /// collection of available patch files.
        /// </summary>
        /// <param name="oldFile">The existing application file to be patched.</param>
        /// <param name="patchFiles">All files available in the patch directory.</param>
        /// <returns>
        /// The matching patch <see cref="FileInfo"/>, or <c>null</c> if no patch exists
        /// for the given file.
        /// </returns>
        FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles);
    }
}
