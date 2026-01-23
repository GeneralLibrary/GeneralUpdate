namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for comparing semantic versions.
    /// </summary>
    public interface ISemVersionComparer
    {
        /// <summary>
        /// Compares two semantic versions.
        /// </summary>
        /// <param name="version1">The first version to compare.</param>
        /// <param name="version2">The second version to compare.</param>
        /// <returns>A value indicating the relative order of the versions.</returns>
        int Compare(SemVersion version1, SemVersion version2);

        /// <summary>
        /// Determines whether a version satisfies a version range.
        /// </summary>
        /// <param name="version">The version to check.</param>
        /// <param name="versionRange">The version range to check against.</param>
        /// <returns>True if the version satisfies the range; otherwise, false.</returns>
        bool Satisfies(SemVersion version, string versionRange);

        /// <summary>
        /// Determines whether two versions are equal.
        /// </summary>
        /// <param name="version1">The first version to compare.</param>
        /// <param name="version2">The second version to compare.</param>
        /// <returns>True if the versions are equal; otherwise, false.</returns>
        bool Equals(SemVersion version1, SemVersion version2);
    }
}
