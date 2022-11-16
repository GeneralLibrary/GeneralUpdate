using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Entity.Assembler;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

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
                var processInfo = SerializeUtil.Deserialize<ProcessInfo>(base64);
                Packet = ProcessAssembler.ToPacket(processInfo);
                Packet.AppType = AppType.UpgradeApp;
                Packet.TempPath = $"{FileUtil.GetTempDirectory(processInfo.LastVersion)}{Path.DirectorySeparatorChar}";
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Client parameter json conversion failed, please check whether the parameter content is legal : { ex.Message },{ ex.StackTrace }.");
            }
            return this;
        }

        public Task<GeneralUpdateBootstrap> LaunchTaskAsync() => Task.Run(() => base.LaunchAsync());
    }
}