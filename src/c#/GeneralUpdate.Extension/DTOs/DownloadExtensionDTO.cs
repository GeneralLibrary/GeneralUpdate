using System.IO;

namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Download extension file data transfer object.
    /// Note: The caller is responsible for disposing the Stream property when done.
    /// </summary>
    public class DownloadExtensionDTO
    {
        /// <summary>
        /// File name with extension
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// File stream. The caller is responsible for disposing this stream.
        /// </summary>
        public Stream Stream { get; set; } = null!;
    }
}
