using GeneralUpdate.Extension.Common.Enums;

namespace GeneralUpdate.Extension.Compatibility;

/// <summary>
/// Abstraction over platform detection, enabling unit testing of platform-dependent logic.
/// </summary>
public interface IPlatformServices
{
    /// <summary>
    /// Detect the current operating system platform.
    /// </summary>
    TargetPlatform GetCurrentPlatform();
}
