﻿using GeneralUpdate.Core.ContentProvider;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Pipelines;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.Middleware;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

        public override void Create<T>(T parameter) => Packet = parameter as Packet;

        public override void Execute()
        {
            Task.Run(async () =>
            {
                try
                {
                    var updateVersions = Packet.UpdateVersions.OrderBy(x => x.PubTime).ToList();
                    if (updateVersions.Count > 0)
                    {
                        foreach (var version in updateVersions)
                        {
                            var patchPath = FileProvider.GetTempDirectory(PATCHS);
                            var zipFilePath = Path.Combine(Packet.TempPath, $"{version.Name}{Packet.Format}");

                            var context = new BaseContext.Builder()
                                .SetVersion(version)
                                .SetZipfilePath(zipFilePath)
                                .SetTargetPath(patchPath)
                                .SetSourcePath(Packet.InstallPath)
                                .SetFormat(Packet.Format)
                                .SetEncoding(Packet.Encoding)
                                .SetBlackFiles(Packet.BlackFiles)
                                .SetBlackFileFormats(Packet.BlackFormats)
                                .SetAppType(Packet.AppType)
                                .Build();

                            var pipelineBuilder = new PipelineBuilder<BaseContext>(context)
                                .UseMiddleware<HashMiddleware>().UseMiddleware<ZipMiddleware>()
                                .UseMiddlewareIf<DriveMiddleware>(Packet.DriveEnabled)
                                .UseMiddleware<PatchMiddleware>();
                            await pipelineBuilder.Build();
                        }

                        if (!string.IsNullOrEmpty(Packet.UpdateLogUrl))
                            Process.Start("explorer.exe", Packet.UpdateLogUrl);
                    }

                    Clear();
                    StartApp(Packet.AppName, Packet.AppType);
                }
                catch (Exception e)
                {
                    EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(e));
                }
            });
        }

        public override bool StartApp(string appName, int appType)
        {
            try
            {
                var path = Path.Combine(Packet.InstallPath, appName);
                switch (appType)
                {
                    case AppType.ClientApp:
                        Environment.SetEnvironmentVariable("ProcessBase64", Packet.ProcessBase64, EnvironmentVariableTarget.User);
                        WaitForProcessToStart(path, 20);
                        break;

                    case AppType.UpgradeApp:
                        WaitForProcessToStart(path, 20);
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

        public override string GetPlatform() => PlatformType.Windows;

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
                if (File.Exists(Packet.TempPath)) File.Delete(Packet.TempPath);
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

        /// <summary>
        /// Waits for the specified process to start within a given time.
        /// </summary>
        /// <param name="applicationPath">Process objects to monitor</param>
        /// <param name="timeout">The maximum interval for waiting for the process to start (The default value is 60 seconds).</param>
        /// <param name="callbackAction"></param>
        private void WaitForProcessToStart(string applicationPath, int timeout, Action callbackAction = null)
        {
            using (var process = Process.Start(applicationPath))
            {
                var startTime = DateTime.UtcNow;
                var timeSpan = TimeSpan.FromSeconds(timeout);
                while (DateTime.UtcNow - startTime < timeSpan)
                {
                    Thread.Sleep(2 * 1000);
                    if (!process.HasExited)
                    {
                        callbackAction?.Invoke();
                        return;
                    }
                }
            }
        }

        #endregion Private Methods
    }
}