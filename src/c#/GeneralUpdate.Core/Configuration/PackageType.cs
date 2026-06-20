namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Defines the type of an update package.
/// Full packages are self-contained and have no local dependency;
/// Chain packages are differential patches that depend on locally installed files.
/// <c>Unspecified</c> (0) is the default value, indicating the type has not been set.
/// </summary>
public enum PackageType
{
    /// <summary>
    /// Not specified / not set. Used as a safe default so that an uninitialized
    /// field (default(int)=0) does not silently imply Chain.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Sequential incremental patch (chain package).
    /// Depends on the previous version being installed. Applied via binary
    /// differential patch (bsdiff/hdiff) over the installed files.
    /// </summary>
    Chain = 1,

    /// <summary>
    /// Full replacement package.
    /// Self-contained; no dependency on any locally installed version.
    /// Applied by extracting the archive directly to the install directory
    /// without any binary patching — a pure overwrite of all files.
    /// </summary>
    Full = 2
}
