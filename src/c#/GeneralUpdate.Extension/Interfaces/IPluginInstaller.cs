using System.Threading.Tasks;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Interfaces
{
    /// <summary>
    /// Handles plugin installation and patch application.
    /// Integrates with GeneralUpdate.Differential for patch restoration.
    /// </summary>
    public interface IPluginInstaller
    {
        /// <summary>
        /// Installs a plugin from a downloaded package.
        /// </summary>
        /// <param name="plugin">Plugin to install.</param>
        /// <param name="packagePath">Path to the downloaded plugin package.</param>
        /// <returns>True if installation succeeded, false otherwise.</returns>
        Task<bool> InstallAsync(PluginInfo plugin, string packagePath);

        /// <summary>
        /// Uninstalls a plugin and removes its files.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>True if uninstallation succeeded, false otherwise.</returns>
        Task<bool> UninstallAsync(string pluginId);

        /// <summary>
        /// Updates an existing plugin using differential patching.
        /// Utilizes GeneralUpdate.Differential.Dirty for patch application.
        /// </summary>
        /// <param name="plugin">Plugin to update.</param>
        /// <param name="patchPath">Path to the patch/update package.</param>
        /// <returns>True if update succeeded, false otherwise.</returns>
        Task<bool> UpdateAsync(PluginInfo plugin, string patchPath);

        /// <summary>
        /// Creates a backup of the current plugin installation before updating.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <returns>Path to the backup, or null if backup failed.</returns>
        Task<string> BackupAsync(string pluginId);

        /// <summary>
        /// Restores a plugin from a backup.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <param name="backupPath">Path to the backup to restore.</param>
        /// <returns>True if restore succeeded, false otherwise.</returns>
        Task<bool> RestoreAsync(string pluginId, string backupPath);

        /// <summary>
        /// Validates a plugin package before installation.
        /// </summary>
        /// <param name="packagePath">Path to the plugin package.</param>
        /// <returns>True if package is valid, false otherwise.</returns>
        Task<bool> ValidatePackageAsync(string packagePath);
    }
}
