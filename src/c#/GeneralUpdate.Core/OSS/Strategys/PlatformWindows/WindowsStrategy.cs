using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GeneralUpdate.Core.OSS.Strategys.PlatformWindows
{
    public class WindowsStrategy : IStrategy
    {
        private string _appPath;

        public void Create(string filePath, string appName)
        {
            throw new NotImplementedException();
        }

        public void Excute()
        {
            throw new NotImplementedException();
        }

        public string GetPlatform() => PlatformType.Windows;

        /// <summary>
        /// Launch the main app.
        /// </summary>
        /// <returns></returns>
        public bool StartApp()
        {
            try
            {
                Process.Start(_appPath);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
