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

        /// <summary>
        /// These files will be skipped when updating.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetBlackFiles() => _blackFiles ?? new List<string>() { "Newtonsoft.Json.dll" };

        /// <summary>
        /// These files that contain the file suffix will be skipped when updating.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetBlackFileFormats() => _blackFileFormats ?? new List<string>() { ".patch", ".7z", ".zip", ".rar", ".tar", ".json" };
    }
}