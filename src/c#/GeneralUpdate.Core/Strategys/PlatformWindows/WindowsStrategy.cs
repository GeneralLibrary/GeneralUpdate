using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Pipelines;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.Middleware;
using GeneralUpdate.Core.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys.PlatformWindows
{
    /// <summary>
    /// Update policy based on the Windows platform.
    /// </summary>
    public class WindowsStrategy : AbstractStrategy
    {
        #region Private Members

        protected Packet Packet { get; set; }
        protected Action<object, MutiDownloadProgressChangedEventArgs> ProgressEventAction { get; set; }
        protected Action<object, ExceptionEventArgs> ExceptionEventAction { get; set; }

        #endregion Private Members

        #region Public Methods

        public override void Create(Entity packet, Action<object, MutiDownloadProgressChangedEventArgs> progressEventAction,
    Action<object, ExceptionEventArgs> exceptionEventAction)
        {
            Packet = (Packet)packet;
            ProgressEventAction = progressEventAction;
            ExceptionEventAction = exceptionEventAction;
        }

        public override void Excute()
        {
            try
            {
                Task.Run(async () =>
                {
                    var updateVersions = Packet.UpdateVersions.OrderBy(x => x.PubTime).ToList();
                    if (updateVersions != null && updateVersions.Count > 0)
                    {
                        foreach (var version in updateVersions)
                        {
                            var patchPath = FileUtil.GetTempDirectory(PATCHS);
                            var zipFilePath = $"{Packet.TempPath}{version.Name}{Packet.Format}";
                            var pipelineBuilder = new PipelineBuilder<BaseContext>(new BaseContext(ProgressEventAction, ExceptionEventAction, version, zipFilePath, patchPath, Packet.InstallPath, Packet.Format, Packet.Encoding)).
                                UseMiddleware<MD5Middleware>().
                                UseMiddleware<ZipMiddleware>().
                                UseMiddleware<PatchMiddleware>();
                            await pipelineBuilder.Launch();
                        }
                    }
                    Clear();
                    StartApp(Packet.AppName, Packet.AppType);
                });
            }
            catch (Exception ex)
            {
                Error(ex);
                return;
            }
        }

        public override bool StartApp(string appName,int appType)
        {
            try
            {
                if (!string.IsNullOrEmpty(Packet.UpdateLogUrl)) Process.Start("explorer.exe", Packet.UpdateLogUrl);
                var path = Path.Combine(Packet.InstallPath, appName);
                switch (appType)
                {
                    case AppType.ClientApp:
                        Process.Start(path, Packet.ProcessBase64);
                        Process.GetCurrentProcess().Kill();
                        break;
                    case AppType.UpgradeApp:
                        Process.Start(path);
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                Error(ex);
                return false;
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void Error(Exception ex)
        { if (ExceptionEventAction != null) ExceptionEventAction(this, new ExceptionEventArgs(ex)); }

        /// <summary>
        /// Remove update redundant files.
        /// </summary>
        /// <returns></returns>
        private bool Clear()
        {
            try
            {
                if (System.IO.File.Exists(Packet.TempPath)) System.IO.File.Delete(Packet.TempPath);
                var dirPath = Path.GetDirectoryName(Packet.TempPath);
                if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
                return true;
            }
            catch (Exception ex)
            {
                if (ExceptionEventAction != null)
                    ExceptionEventAction(this, new ExceptionEventArgs(ex));
                return false;
            }
        }

        #endregion Private Methods

        public override string GetPlatform()=> PlatformType.Windows;
    }
}
