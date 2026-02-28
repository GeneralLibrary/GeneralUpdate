using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Differential.Matchers
{
    /// <summary>
    /// Default implementation of <see cref="IDirtyMatcher"/> that preserves the
    /// original matching behaviour of <c>DifferentialCore.Dirty</c>:
    /// a patch file matches an application file when the patch file's name
    /// (without the <c>.patch</c> extension) equals the application file's name,
    /// and the patch file carries the <c>.patch</c> extension.
    /// </summary>
    public class DefaultDirtyMatcher : IDirtyMatcher
    {
        private const string PatchFormat = ".patch";

        /// <inheritdoc/>
        public FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles)
        {
            var findFile = patchFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f.Name).Replace(PatchFormat, "").Equals(oldFile.Name));

            if (findFile != null && string.Equals(Path.GetExtension(findFile.FullName), PatchFormat))
                return findFile;

            return null;
        }
    }
}
