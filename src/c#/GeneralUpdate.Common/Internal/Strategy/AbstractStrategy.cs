using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.Strategy
{
    public abstract class AbstractStrategy : IStrategy
    {
        protected const string PATCHS = "patchs";

        public virtual void Execute() => throw new NotImplementedException();
        
        public virtual Task ExecuteAsync() => throw new NotImplementedException();

        public virtual void StartApp(string appName, int appType) => throw new NotImplementedException();
        
        public virtual Task ExecuteTaskAsync() => throw new NotImplementedException();

        public virtual void Create(Packet parameter) => throw new NotImplementedException();

        protected void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported OS platform");
            }
        }
    }
}