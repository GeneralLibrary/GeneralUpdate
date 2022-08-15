using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;

namespace GeneralUpdate.Core
{
    public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
    {
        public GeneralUpdateBootstrap() : base(){}

        /// <summary>
        /// Set parameter.
        /// </summary>
        /// <param name="base64">ClientParameter object to base64 string.</param>
        /// <returns></returns>
        public GeneralUpdateBootstrap Remote(string base64)
        {
            try
            {
                var entity = SerializeUtil.Deserialize<ProcessInfo>(base64);
                var tempPath = $"{FileUtil.GetTempDirectory(entity.LastVersion)}\\";
                var encoding = ConvertUtil.ToEncoding(entity.CompressEncoding);
                Packet = new Packet(
                    entity.MainUpdateUrl, entity.AppType, entity.UpdateUrl, 
                    entity.AppName, entity.MainAppName, entity.CompressFormat, 
                    entity.IsUpdate, entity.UpdateLogUrl, encoding,entity.DownloadTimeOut,
                    entity.AppSecretKey, tempPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Client parameter json conversion failed, please check whether the parameter content is legal : { ex.Message },{ ex.StackTrace }.");
            }
            return this;
        }
    }
}