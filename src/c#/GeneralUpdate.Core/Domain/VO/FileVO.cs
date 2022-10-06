using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Domain.VO
{
    /// <summary>
    /// file object value.
    /// </summary>
    public class FileVO
    {
        /// <summary>
        /// Client current version.
        /// </summary>
        public string ClientVersion { get; set; }

        /// <summary>
        /// The latest version.
        /// </summary>
        public string LastVersion { get; set; }

        /// <summary>
        /// installation path (for update file logic).
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Download file temporary storage path (for update file logic).
        /// </summary>
        public string TempPath { get; set; }
    }
}
