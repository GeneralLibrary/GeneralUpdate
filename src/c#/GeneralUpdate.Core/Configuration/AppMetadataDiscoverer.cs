using System;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Fills missing identity fields in a <see cref="UpdateRequest"/> by probing
///     <c>generalupdate.manifest.json</c>, then falling back to hard-coded defaults.
/// </summary>
public static class AppMetadataDiscoverer
{
    /// <summary>
    ///     Discover and fill every null-or-empty identity field in <paramref name="seed"/>.
    /// </summary>
    public static UpdateRequest Discover(UpdateRequest seed)
    {
        if (seed == null)
            throw new System.ArgumentNullException(nameof(seed));

        var manifest = ManifestInfo.Load();

        // Identity fields — manifest overrides defaults, not just nulls.
        // (UpdateConfiguration pre-fills UpdateAppName with "Update.exe", so simple
        //  null-coalescing would never pick up the manifest value.)
        if (manifest != null)
        {
            if (!string.IsNullOrWhiteSpace(manifest.MainAppName))
                seed.MainAppName = manifest.MainAppName;
            if (!string.IsNullOrWhiteSpace(manifest.UpdateAppName))
                seed.UpdateAppName = manifest.UpdateAppName;
            if (!string.IsNullOrWhiteSpace(manifest.ClientVersion))
                seed.ClientVersion = manifest.ClientVersion;
            if (!string.IsNullOrWhiteSpace(manifest.UpgradeClientVersion))
                seed.UpgradeClientVersion = manifest.UpgradeClientVersion;
            if (!string.IsNullOrWhiteSpace(manifest.ProductId))
                seed.ProductId = manifest.ProductId;
            if (!string.IsNullOrWhiteSpace(manifest.UpdatePath))
                seed.UpdatePath = manifest.UpdatePath;
            if (!string.IsNullOrWhiteSpace(manifest.AppType)
                && Enum.TryParse<AppType>(manifest.AppType, out var at))
                seed.AppType = at;
        }

        // Hard-coded fallbacks only when neither seed nor manifest provided a value.
        seed.MainAppName ??= "Client";
        seed.UpdateAppName ??= "Update.exe";
        seed.InstallPath ??= AppDomain.CurrentDomain.BaseDirectory;

        return seed;
    }
}
