using System.Collections.Generic;

namespace GeneralUpdate.Differential.Common
{
    /// <summary>
    /// Used to filter out non-updatable file formats during the update process.
    /// </summary>
    public class Filefilter
    {
        private static List<string> _blackFiles, _blackFileFormats;

        /// <summary>
        /// Set a blacklist.
        /// </summary>
        /// <param name="blackFiles">A collection of blacklist files that are skipped when updated.</param>
        /// <param name="blackFileFormats">A collection of blacklist file name extensions that are skipped on update.</param>
        public static void SetBlacklist(List<string> blackFiles, List<string> blackFileFormats)
        {
            _blackFiles = blackFiles;
            _blackFileFormats = blackFileFormats;
        }

        public static List<string> GetBlackFiles() => _blackFiles;

        public static List<string> GetBlackFileFormats() => _blackFileFormats;
    }
}