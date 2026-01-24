using System;

namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents the platform on which an extension can run.
    /// </summary>
    [Flags]
    public enum ExtensionPlatform
    {
        None = 0,
        Windows = 1,
        Linux = 2,
        macOS = 4,
        All = Windows | Linux | macOS
    }
}
