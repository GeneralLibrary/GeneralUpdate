using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Core
{
    public class GeneralUpdateBootstrap : AbstractBootstrap<GeneralUpdateBootstrap, IStrategy>
    {
        private Packet Packet { get; set; }
        
        public GeneralUpdateBootstrap() : base() => Remote();

        /// <summary>
        /// Gets values from system environment variables (ClientParameter object to base64 string).
        /// </summary>
        private void Remote()
        {
            try
            {
                
                var json = Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.User);
                var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json);
                Packet = null;
                Packet.AppType = AppType.UpgradeApp;
                Packet.TempPath = $"{GeneralFileManager.GetTempDirectory(processInfo.LastVersion)}{Path.DirectorySeparatorChar}";
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Client parameter json conversion failed, please check whether the parameter content is legal : {ex.Message},{ex.StackTrace}.");
            }
        }

        public override Task<GeneralUpdateBootstrap> LaunchAsync()
        {
            throw new NotImplementedException();
        }

        protected override void ExecuteStrategy()
        {
            throw new NotImplementedException();
        }

        protected override GeneralUpdateBootstrap StrategyFactory()
        {
            throw new NotImplementedException();
        }
    }
}