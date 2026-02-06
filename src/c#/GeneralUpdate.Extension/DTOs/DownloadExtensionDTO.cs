using System.IO;

namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Download extension file data transfer object
    /// </summary>
    public class DownloadExtensionDTO
    {
        /// <summary>
        /// File name with extension
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// File stream
        /// </summary>
        public Stream Stream { get; set; } = null!;
    }
}
