using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
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

        #endregion Private Members

        #region Public Methods

        public override void Create<T>(T parameter)
        {
            Packet = parameter as Packet;
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
                            var pipelineBuilder = new PipelineBuilder<BaseContext>(new BaseContext(version, zipFilePath, patchPath, Packet.InstallPath, Packet.Format, Packet.Encoding)).
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
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return;
            }
        }

        public override bool StartApp(string appName, int appType)
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
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return false;
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        #endregion Public Methods

        #region Private Methods

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
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return false;
            }
        }

        #endregion Private Methods

        public override string GetPlatform() => PlatformType.Windows;
    }
}