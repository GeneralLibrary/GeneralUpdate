using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Entity.Assembler;
using GeneralUpdate.Core.Domain.Service;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore
{
    public sealed class GeneralClientOSS
    {
        private static Func<bool> _customOption;
        private static Func<Task<bool>> _customTaskOption;

        private GeneralClientOSS() { }

        /// <summary>
        /// Starting an OSS update for windows,linux,mac platform.
        /// </summary>
        /// <param name="configInfo"></param>
        public static async Task Start(ParamsOSS configParams,string upgradeAppName = "GeneralUpdate.Upgrad")
        {
            try
            {
                //TODO: 下载指定文件下载失败则抛出异常
                var path = Path.Combine(System.Threading.Thread.GetDomain().BaseDirectory, $"{upgradeAppName}.exe");
                if (!File.Exists(path)) throw new Exception($"The application does not exist {upgradeAppName} !");
                var processBase64 = ProcessAssembler.ToBase64(configParams);
                Process.Start(path, processBase64);
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                throw new Exception($"GeneralClientOSS update exception ! {ex.Message}", ex.InnerException);
            }
        }
    }
}
