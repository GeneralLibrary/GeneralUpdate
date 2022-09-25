using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.DTO.Assembler;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Entity.Assembler;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Domain.Service;
using GeneralUpdate.Core.Strategys;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace GeneralUpdate.ClientCore
{
    public class GeneralClientBootstrap : AbstractBootstrap<GeneralClientBootstrap, IStrategy>
    {
        private Func<bool> _customOption;

        public GeneralClientBootstrap() : base() { }

        #region Public Methods

        public override GeneralClientBootstrap LaunchAsync()
        {
            Task.Run(() => BaseLaunch());
            return this;
        }

        public Task<GeneralClientBootstrap> LaunchTaskAsync() => BaseLaunch();

        private async Task<GeneralClientBootstrap> BaseLaunch() 
        {
            var versionService = new VersionService();
            var mainResp = await versionService.ValidationVersion(Packet.MainUpdateUrl);
            var upgradResp = await versionService.ValidationVersion(Packet.UpdateUrl);
            Packet.IsUpgradeUpdate = upgradResp.Body.IsUpdate;
            Packet.IsMainUpdate = mainResp.Body.IsUpdate;
            //No need to update, return directly.
            if ((!Packet.IsMainUpdate) && (!Packet.IsUpgradeUpdate)) return this;
            //If the main program needs to be forced to update, the skip will not take effect.
            var isForcibly = mainResp.Body.IsForcibly || upgradResp.Body.IsForcibly;
            if (IsSkip(isForcibly)) return this;
            Packet.UpdateVersions = VersionAssembler.ToEntitys(upgradResp.Body.Versions);
            Packet.LastVersion = Packet.UpdateVersions.Last().Version;
            var processInfo = new ProcessInfo(Packet.MainAppName, Packet.InstallPath,
                    Packet.ClientVersion, Packet.LastVersion, Packet.UpdateLogUrl,
                    Packet.Encoding, Packet.Format,Packet.DownloadTimeOut,
                    Packet.AppSecretKey, mainResp.Body.Versions);
            Packet.ProcessBase64 = ProcessAssembler.ToBase64(processInfo);
            return base.LaunchAsync();
        }

        /// <summary>
        /// Configure server address .
        /// </summary>
        /// <param name="url">Remote server address.</param>
        /// <param name="appName">The updater name does not need to contain an extension.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Parameter initialization is abnormal.</exception>
        public GeneralClientBootstrap Config(string url,string appSecretKey, string appName = "GeneralUpdate.Upgrad")
        {
            if (string.IsNullOrEmpty(url)) throw new Exception("Url cannot be empty !");
            try
            {
                string basePath = System.Threading.Thread.GetDomain().BaseDirectory; //AppDomain.CurrentDomain.BaseDirectory;
                Packet.InstallPath = basePath;
                Packet.AppSecretKey = appSecretKey;
                //update app.
                Packet.AppName = appName;
                string clienVersion = GetFileVersion(Path.Combine(basePath, $"{Packet.AppName}.exe"));
                Packet.ClientVersion = clienVersion;
                Packet.AppType = AppType.ClientApp;
                Packet.UpdateUrl = $"{url}/versions/{ Packet.AppType }/{ clienVersion }/{ Packet.AppSecretKey }";
                //main app.
                string mainAppName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                string mainVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Packet.MainUpdateUrl = $"{url}/versions/{ AppType.ClientApp }/{ mainVersion }/{Packet.AppSecretKey}";
                Packet.MainAppName = mainAppName;
                return this;
            }
            catch (Exception ex)
            {
                throw new Exception($"Initial configuration parameters are abnormal . {  ex.Message }", ex.InnerException);
            }
        }

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
        /// <param name="func">Custom funcion ,Custom actions to let users decide whether to update. true update false do not update .</param>
        /// <returns></returns>
        public GeneralClientBootstrap SetCustomOption(Func<bool> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            _customOption = func;
            return this;
        }

        #endregion

        #region Private Methods

        private string GetFileVersion(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo != null && fileInfo.Exists) return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
                throw new Exception($"Failed to obtain file '{filePath}' version. Procedure.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to obtain file '{filePath}' version. Procedure. Eorr message : {ex.Message} .", ex.InnerException);
            }
        }

        /// <summary>
        /// User decides if update is required.
        /// </summary>
        /// <returns>is false to continue execution.</returns>
        private bool IsSkip(bool isForcibly) 
        {
            bool isSkip = false;
            if (isForcibly) return false;
            if (_customOption != null) isSkip = _customOption.Invoke();
            return isSkip;
        }

        #endregion
    }
}