using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.ContentProvider;
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
        public GeneralUpdateBootstrap() : base() => Remote();

        /// <summary>
        /// Gets values from system environment variables (ClientParameter object to base64 string).
        /// </summary>
        private void Remote()
        {
            try
            {
                var base64 = Environment.GetEnvironmentVariable("ProcessBase64", EnvironmentVariableTarget.User);
                var processInfo = SerializeUtil.Deserialize<ProcessInfo>(base64);
                Packet = ProcessAssembler.ToPacket(processInfo);
                Packet.AppType = AppType.UpgradeApp;
                Packet.TempPath = $"{FileProvider.GetTempDirectory(processInfo.LastVersion)}{Path.DirectorySeparatorChar}";
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Client parameter json conversion failed, please check whether the parameter content is legal : {ex.Message},{ex.StackTrace}.");
            }
        }

        /// <summary>
        /// Start the update.
        /// </summary>
        /// <returns></returns>
        public Task<GeneralUpdateBootstrap> LaunchTaskAsync() => Task.Run(() => base.LaunchAsync());
    }
}