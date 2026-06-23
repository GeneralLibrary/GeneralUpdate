namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Compression format for update packages.
    /// </summary>
    public enum Format
    {
        /// <summary>ZIP compression format.</summary>
        Zip
    }

    /// <summary>
    /// Extension methods for <see cref="Format"/>.
    /// </summary>
    public static class FormatExtensions
    {
        /// <summary>
        /// Returns the file extension for the format (including leading dot).
        /// </summary>
        public static string ToExtension(this Format format) => format switch
        {
            Format.Zip => ".zip",
            _ => ".zip"
        };
    }
}
