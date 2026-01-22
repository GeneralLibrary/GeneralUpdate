namespace MyApp.Extensions.Updates
{
    /// <summary>
    /// Represents the update channel for an extension.
    /// </summary>
    public enum UpdateChannel
    {
        /// <summary>
        /// Stable release channel for production-ready updates.
        /// </summary>
        Stable,

        /// <summary>
        /// Pre-release channel for beta or release candidate versions.
        /// </summary>
        PreRelease,

        /// <summary>
        /// Development channel for cutting-edge, experimental updates.
        /// </summary>
        Dev
    }
}
