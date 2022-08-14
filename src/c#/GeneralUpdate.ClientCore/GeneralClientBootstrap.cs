using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore
{
    public class GeneralClientBootstrap : AbstractBootstrap<GeneralClientBootstrap, IStrategy>
    {
        private Func<bool> _customOption;

        public GeneralClientBootstrap() : base() { }

        #region Public Methods

        public override async Task<GeneralClientBootstrap> LaunchTaskAsync()
        {
            try
            {
                //Verify whether 'upgrad' needs to be updated.
                var respDTO = await HttpUtil.GetTaskAsync<VersionRespDTO>(Packet.MainUpdateUrl);
                if (respDTO == null) throw new ArgumentNullException("The verification request is abnormal, please check the network or parameter configuration!");
                if (respDTO.Code != HttpStatus.OK) throw new Exception($"Request failed , Code :{ respDTO.Code }, Message:{ respDTO.Message } !");
                if (respDTO.Code == HttpStatus.OK)
                {
                    var body = respDTO.Body;
                    if (body.IsForcibly || body.IsUpdate) Packet.IsUpdate = body.IsForcibly;

                    if (Packet.IsUpdate)
                    {
                        //Do you need to force an update.
                        if (body.IsForcibly)
                        {
                            await base.LaunchTaskAsync();
                        }
                        else if (body.IsUpdate)//Does it need to be updated.
                        {
                            bool isSkip = false;
                            //User decides if update is required.
                            if (_customOption != null) isSkip = _customOption.Invoke();
                            if (isSkip) await base.LaunchTaskAsync();
                        }
                    }
                    else 
                    {
                        base.Launch0();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return await Task.FromResult(this);
        }

        /// <summary>
        /// Configure server address .
        /// </summary>
        /// <param name="url">Remote server address.</param>
        /// <param name="appName">The updater name does not need to contain an extension.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Parameter initialization is abnormal.</exception>
        public GeneralClientBootstrap Config(string url,string appSecretKey, string appName = "AutoUpdate.Core")
        {
            if (string.IsNullOrEmpty(url)) throw new Exception("Url cannot be empty !");
            try
            {
                string basePath = Environment.CurrentDirectory;
                Packet.InstallPath = basePath;
                Packet.IsUpdate = true;
                Packet.AppSecretKey = appSecretKey;
                //update app.
                Packet.AppName = appName;
                string clienVersion = GetFileVersion(Path.Combine(basePath, Packet.AppName + ".exe"));
                Packet.ClientVersion = clienVersion;
                Packet.AppType = (int)AppType.UpdateApp;
                Packet.UpdateUrl = $"{url}/versions/{ Packet.AppType }/{ clienVersion }/{ Packet.AppSecretKey }";
                //main app.
                string mainAppName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                string mainVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Packet.MainUpdateUrl = $"{url}/versions/{ (int)AppType.ClientApp }/{ mainVersion }/{Packet.AppSecretKey}";
                Packet.MainAppName = mainAppName;
                return this;
            }
            catch (Exception ex)
            {
                throw new Exception($"Initial configuration parameters are abnormal . {  ex.Message }", ex.InnerException);
            }
        }

        public GeneralClientBootstrap Config(ProcessEntity entity)
        {
            Packet.ClientVersion = entity.ClientVersion;
            Packet.AppType = entity.AppType;
            Packet.UpdateUrl = entity.UpdateUrl;
            Packet.MainUpdateUrl = entity.MainUpdateUrl;
            Packet.AppName = entity.AppName;
            Packet.MainAppName = entity.MainAppName;
            Packet.InstallPath = entity.InstallPath;
            Packet.UpdateLogUrl = entity.UpdateLogUrl;
            Packet.IsUpdate = entity.IsUpdate;
            Packet.AppSecretKey = entity.AppSecretKey;
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
                throw new Exception($"Failed to obtain file '{ filePath }' version. Procedure.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to obtain file '{ filePath }' version. Procedure. Eorr message : { ex.Message } .", ex.InnerException);
            }
        }

        #endregion
    }
}