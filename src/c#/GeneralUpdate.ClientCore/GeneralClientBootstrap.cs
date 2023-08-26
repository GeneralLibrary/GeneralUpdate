using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.DTO.Assembler;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Entity.Assembler;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Domain.Service;
using GeneralUpdate.Core.Exceptions.CustomArgs;
using GeneralUpdate.Core.Exceptions.CustomException;
using GeneralUpdate.Core.Strategys;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore
{
    /// <summary>
    /// This component is used only for client application bootstrapping classes.
    /// </summary>
    public class GeneralClientBootstrap : AbstractBootstrap<GeneralClientBootstrap, IStrategy>
    {
        private Func<bool> _customOption;
        private Func<Task<bool>> _customTaskOption;

        public GeneralClientBootstrap() : base()
        {
        }

        #region Public Methods

        /// <summary>
        /// Start the update.
        /// </summary>
        /// <returns></returns>
        public override GeneralClientBootstrap LaunchAsync()
        {
            Task.Run(() => BaseLaunch());
            return this;
        }

        /// <summary>
        /// Start the update.
        /// </summary>
        /// <returns></returns>
        public Task<GeneralClientBootstrap> LaunchTaskAsync() => BaseLaunch();

        private async Task<GeneralClientBootstrap> BaseLaunch()
        {
            var versionService = new VersionService();
            var mainResp = await versionService.ValidationVersion(Packet.MainUpdateUrl);
            var upgradeResp = await versionService.ValidationVersion(Packet.UpdateUrl);
            Packet.IsUpgradeUpdate = upgradeResp.Body.IsUpdate;
            Packet.IsMainUpdate = mainResp.Body.IsUpdate;
            //No need to update, return directly.
            if ((!Packet.IsMainUpdate) && (!Packet.IsUpgradeUpdate)) return this;
            //If the main program needs to be forced to update, the skip will not take effect.
            var isForcibly = mainResp.Body.IsForcibly || upgradeResp.Body.IsForcibly;
            if (await IsSkip(isForcibly)) return this;
            Packet.UpdateVersions = VersionAssembler.ToEntitys(upgradeResp.Body.Versions);
            Packet.LastVersion = Packet.UpdateVersions.Last().Version;
            var processInfo = new ProcessInfo(Packet.MainAppName, Packet.InstallPath,
                    Packet.ClientVersion, Packet.LastVersion, Packet.UpdateLogUrl,
                    Packet.Encoding, Packet.Format, Packet.DownloadTimeOut,
                    Packet.AppSecretKey, mainResp.Body.Versions);
            Packet.ProcessBase64 = ProcessAssembler.ToBase64(processInfo);
            return base.LaunchAsync();
        }

        /// <summary>
        /// Configure server address (Recommended Windows,Linux,Mac).
        /// </summary>
        /// <param name="url">Remote server address.</param>
        /// <param name="appName">The updater name does not need to contain an extension.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Parameter initialization is abnormal.</exception>
        public GeneralClientBootstrap Config(string url, string appSecretKey, string appName = "GeneralUpdate.Upgrade")
        {
            if (string.IsNullOrEmpty(url)) throw new Exception("Url cannot be empty !");
            try
            {
                string basePath = System.Threading.Thread.GetDomain().BaseDirectory;
                Packet.InstallPath = basePath;
                Packet.AppSecretKey = appSecretKey;
                //update app.
                Packet.AppName = appName;
                string clienVersion = GetFileVersion(Path.Combine(basePath, $"{Packet.AppName}.exe"));
                Packet.ClientVersion = clienVersion;
                Packet.AppType = AppType.ClientApp;
                Packet.UpdateUrl = $"{url}/versions/{AppType.ClientApp}/{clienVersion}/{Packet.AppSecretKey}";
                //main app.
                string mainAppName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                string mainVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Packet.MainUpdateUrl = $"{url}/versions/{AppType.ClientApp}/{mainVersion}/{Packet.AppSecretKey}";
                Packet.MainAppName = mainAppName;
                return this;
            }
            catch (Exception ex)
            {
                throw new GeneralUpdateException<ExceptionArgs>(ex.Message, ex.InnerException);
            }
        }

        /// <summary>
        /// Custom Configuration (Recommended : All platforms).
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public GeneralClientBootstrap Config(Configinfo info)
        {
            Packet.AppType = info.AppType;
            Packet.AppName = info.AppName;
            Packet.AppSecretKey = info.AppSecretKey;
            Packet.ClientVersion = info.ClientVersion;
            Packet.UpdateUrl = info.UpdateUrl;
            Packet.MainUpdateUrl = info.MainUpdateUrl;
            Packet.MainAppName = info.MainAppName;
            Packet.InstallPath = info.InstallPath;
            Packet.UpdateLogUrl = info.UpdateLogUrl;
            return this;
        }

        /// <summary>
        /// Let the user decide whether to update in the state of non-mandatory update.
        /// </summary>
        /// <param name="func">Custom function ,Custom actions to let users decide whether to update. true update false do not update .</param>
        /// <returns></returns>
        public GeneralClientBootstrap SetCustomOption(Func<bool> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            _customOption = func;
            return this;
        }

        /// <summary>
        ///  Let the user decide whether to update in the state of non-mandatory update.
        /// </summary>
        /// <param name="func">Custom function ,Custom actions to let users decide whether to update. true update false do not update .</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public GeneralClientBootstrap SetCustomOption(Func<Task<bool>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            _customTaskOption = func;
            return this;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        ///Gets the application version number
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="GeneralUpdateException{ExceptionArgs}"></exception>
        private string GetFileVersion(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo != null && fileInfo.Exists) return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
            throw new GeneralUpdateException<ExceptionArgs>($"Failed to obtain file '{filePath}' version. Procedure.");
        }

        /// <summary>
        /// User decides if update is required.
        /// </summary>
        /// <returns>is false to continue execution.</returns>
        private async Task<bool> IsSkip(bool isForcibly)
        {
            try
            {
                bool isSkip = false;
                if (isForcibly) return false;
                if (_customTaskOption != null) isSkip = await _customTaskOption.Invoke();
                if (_customOption != null) isSkip = _customOption.Invoke();
                return isSkip;
            }
            catch (Exception ex)
            {
                throw new GeneralUpdateException<ExceptionArgs>($"The injected user skips this update with an exception ! {ex.Message}", ex.InnerException);
            }
        }

        #endregion Private Methods
    }
}