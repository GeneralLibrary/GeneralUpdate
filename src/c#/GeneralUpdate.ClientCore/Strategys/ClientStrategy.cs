using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys;
using GeneralUpdate.Core.Strategys.PlatformAndroid;
using GeneralUpdate.Core.Strategys.PlatformiOS;
using GeneralUpdate.Core.Strategys.PlatformLinux;
using GeneralUpdate.Core.Strategys.PlatformMac;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GeneralUpdate.ClientCore.Strategys
{
    public class ClientStrategy : AbstractStrategy
    {
        #region Private Members

        private Dictionary<string, IStrategy> _strategys;
        private IStrategy _currentStrategy;

        #endregion

        #region Protected Properties

        protected Action<object, MutiDownloadProgressChangedEventArgs> ProgressEventAction { get; set; }
        protected Action<object, ExceptionEventArgs> ExceptionEventAction { get; set; }
        protected Packet Packet { get; set; }

        protected Dictionary<string, IStrategy> Strategys
        {
            get
            {
                if (_strategys == null)
                {
                    _strategys.Add(PlatformType.Windows, new WindowsStrategy());
                    _strategys.Add(PlatformType.Linux, new LinuxStrategy());
                    _strategys.Add(PlatformType.Mac, new MacStrategy());
                    _strategys.Add(PlatformType.Android, new AndroidStrategy());
                    _strategys.Add(PlatformType.iOS, new iOSStrategy());
                }
                return _strategys;
            }
        }

        #endregion

        #region Public Methods

        public override void Create(Entity entity, Action<object, MutiDownloadProgressChangedEventArgs> progressEventAction, Action<object, ExceptionEventArgs> exceptionEventAction)
        {
            Packet = (Packet)entity;
            ProgressEventAction = progressEventAction;
            ExceptionEventAction = exceptionEventAction;
            IStrategy tempStrategy = null;
            Strategys.TryGetValue(Packet.Platform, out tempStrategy);
            _currentStrategy = tempStrategy;
        }

        public override void Excute() => _currentStrategy.Excute();

        public override string GetPlatform() => _currentStrategy.GetPlatform();

        #endregion

        #region Private Methods

        protected override bool StartApp(string appName)
        {
            try
            {
                if (!string.IsNullOrEmpty(Packet.UpdateLogUrl) && GetPlatform() == PlatformType.Windows) 
                    Process.Start("explorer.exe", Packet.UpdateLogUrl);
                Process.Start(Path.Combine(Packet.InstallPath, appName), Packet.ProcessBase64);
                Process.GetCurrentProcess().Kill();
                return true;
            }
            catch (Exception ex)
            {
                if (ExceptionEventAction != null)
                    ExceptionEventAction(this, new ExceptionEventArgs(ex));
                return false;
            }
        }

        #endregion
    }
}