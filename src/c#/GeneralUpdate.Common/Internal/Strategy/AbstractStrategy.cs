using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.Strategy
{
    public abstract class AbstractStrategy : IStrategy
    {
        protected const string Patchs = "patchs";
        
        public virtual Task Execute() => throw new NotImplementedException();
        
        public virtual void StartApp() => throw new NotImplementedException();
        
        public virtual Task ExecuteAsync() => throw new NotImplementedException();

        public virtual void Create(GlobalConfigInfo parameter) => throw new NotImplementedException();

        protected static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                return;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
                return;
            }
            
            throw new PlatformNotSupportedException("Unsupported OS platform");
        }
        
        protected static void Clear(string path)
        {
            if (Directory.Exists(path))
                StorageManager.DeleteDirectory(path);
        }
    }
}