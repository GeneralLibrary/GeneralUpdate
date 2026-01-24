using System;

namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Defines the target platforms for extension deployment.
    /// Uses flags pattern to support multiple platform combinations.
    /// </summary>
    [Flags]
    public enum TargetPlatform
    {
        /// <summary>
        /// No platform specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Windows operating system.
        /// </summary>
        Windows = 1,

        /// <summary>
        /// Linux operating system.
        /// </summary>
        Linux = 2,

        /// <summary>
        /// macOS operating system.
        /// </summary>
        MacOS = 4,

        /// <summary>
        /// All supported platforms (Windows, Linux, and macOS).
        /// </summary>
        All = Windows | Linux | MacOS
    }
}
