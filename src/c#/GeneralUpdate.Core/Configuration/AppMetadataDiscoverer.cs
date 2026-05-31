using System;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Fills missing identity fields in a <see cref="UpdateRequest"/> by probing
///     <c>generalupdate.manifest.json</c>, then falling back to hard-coded defaults.
/// </summary>
public static class AppMetadataDiscoverer
{
    /// <summary>
    ///     Discover and fill every null-or-empty identity field in <paramref name="context"/>
    ///     from <c>generalupdate.manifest.json</c>. Called by <see cref="ClientStrategy"/>
    ///     during <c>ExecuteStandardWorkflowAsync</c> so that manifest discovery is owned
    ///     by the strategy rather than the bootstrap.
    /// </summary>
    public static void Discover(UpdateContext context)
    {
        if (context == null)
            throw new System.ArgumentNullException(nameof(context));

        var installPath = context.InstallPath;
        var manifestPath = System.IO.Path.Combine(installPath, ManifestInfo.FileName);
        var manifest = ManifestInfo.Load(manifestPath);

        if (manifest == null) return;

        if (!string.IsNullOrWhiteSpace(manifest.MainAppName))
            context.MainAppName = manifest.MainAppName;
        if (!string.IsNullOrWhiteSpace(manifest.UpdateAppName))
            context.UpdateAppName = manifest.UpdateAppName;
        if (!string.IsNullOrWhiteSpace(manifest.ClientVersion))
            context.ClientVersion = manifest.ClientVersion;
        if (!string.IsNullOrWhiteSpace(manifest.UpgradeClientVersion))
            context.UpgradeClientVersion = manifest.UpgradeClientVersion;
        if (!string.IsNullOrWhiteSpace(manifest.ProductId))
            context.ProductId = manifest.ProductId;
        if (!string.IsNullOrWhiteSpace(manifest.UpdatePath))
            context.UpdatePath = manifest.UpdatePath;
        if (!string.IsNullOrWhiteSpace(manifest.AppType)
            && Enum.TryParse<AppType>(manifest.AppType, out var at))
            context.AppType = at;
    }
}
