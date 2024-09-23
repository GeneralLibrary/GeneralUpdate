using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategys
{
    /// <summary>
    /// Update policy based on the Windows platform.
    /// </summary>
    public class WindowsStrategy : AbstractStrategy
    {
        private Packet Packet { get; set; }

        #region Private Methods

        /// <summary>
        ///     Remove update redundant files.
        /// </summary>
        /// <returns></returns>
        private void Clear()
        {
            if (File.Exists(Packet.TempPath)) File.Delete(Packet.TempPath);
            var dirPath = Path.GetDirectoryName(Packet.TempPath);
            if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
        }

        #endregion Private Methods

        #region Public Methods

        public override void Create(Packet parameter)
        {
            Packet = parameter;
        }

        public override async Task ExecuteAsync()
        {
            var updateVersions = Packet.UpdateVersions.OrderBy(x => x.PubTime).ToList();
            if (updateVersions.Count > 0)
            {
                foreach (var version in updateVersions)
                {
                    var patchPath = GeneralFileManager.GetTempDirectory(PATCHS);
                    var zipFilePath = Path.Combine(Packet.TempPath, $"{version.Name}{Packet.Format}");

                    var context = new PipelineContext();
                    //hash middleware
                    context.Add("Hash", version.Hash);
                    context.Add("FileName", zipFilePath);
                    //zip middleware
                    context.Add("Format", Packet.Format);
                    context.Add("Name", zipFilePath);
                    context.Add("SourcePath", Packet.TempPath);
                    context.Add("DestinationPath", Packet.InstallPath);
                    context.Add("Encoding", Packet.Encoding);
                    //patch middleware
                    context.Add("SourcePath", patchPath);
                    context.Add("TargetPath", Packet.InstallPath);
                    context.Add("BlackFiles", BlackListManager.Instance.BlackFiles);
                    context.Add("BlackFileFormats", BlackListManager.Instance.BlackFileFormats);
                    //driver middleware
                    context.Add("DriverPath", new List<string>());
                    context.Add("Version", version.Version);
                    

                    var pipelineBuilder = new PipelineBuilder(context)
                        .UseMiddleware<HashMiddleware>()
                        .UseMiddleware<ZipMiddleware>()
                        .UseMiddleware<PatchMiddleware>()
                        .UseMiddlewareIf<DriverMiddleware>(()=> Packet.DriveEnabled);
                    await pipelineBuilder.Build();
                }

                if (!string.IsNullOrEmpty(Packet.UpdateLogUrl))
                    OpenBrowser(Packet.UpdateLogUrl);
            }

            Clear();
            StartApp(Packet.AppName, Packet.AppType);
        }

        public override void StartApp(string appName, int appType)
        {
            var path = Path.Combine(Packet.InstallPath, appName);
            switch (appType)
            {
                case AppType.ClientApp:
                    Environment.SetEnvironmentVariable("ProcessInfo", Packet.ProcessInfo,
                        EnvironmentVariableTarget.User);
                    Process.Start(path);
                    break;

                case AppType.UpgradeApp:
                    Process.Start(path);
                    break;

                default:
                    throw new ArgumentException("Invalid app type");
            }
        }

        #endregion Public Methods
    }
}