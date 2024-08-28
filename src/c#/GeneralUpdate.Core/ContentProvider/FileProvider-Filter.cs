using System.Collections.Generic;

namespace GeneralUpdate.Core.ContentProvider
{
    public partial class FileProvider
    {
        private static List<string> _blackFiles,
            _blackFileFormats;
        private static readonly List<string> DefaultBlackFileFormats = new List<string>(6)
        {
            ".patch",
            ".7z",
            ".zip",
            ".rar",
            ".tar",
            ".json"
        };
        private static readonly List<string> DefaultBlackFiles = new List<string>(1) { "Newtonsoft.Json.dll" };

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
        public static List<string> GetBlackFiles() =>
            _blackFiles ?? DefaultBlackFiles;

        /// <summary>
        /// These files that contain the file suffix will be skipped when updating.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetBlackFileFormats() =>
            _blackFileFormats ?? DefaultBlackFileFormats;
    }
}
