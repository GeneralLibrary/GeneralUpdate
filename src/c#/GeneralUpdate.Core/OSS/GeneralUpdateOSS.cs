using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Exceptions;
using GeneralUpdate.Core.OSS.Strategys;
using GeneralUpdate.Core.OSS.Strategys.PlatformAndorid;
using GeneralUpdate.Core.OSS.Strategys.PlatformWindows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public sealed class GeneralUpdateOSS
    {
        public static async Task Start<T>(string url,string appName , string platform = PlatformType.Windows) where T : IOSS, new()
        {
            IStrategy strategy = null;
            var oss = new T();
            oss.SetParmeter(url);
            string filePath = await oss.Download();
            switch (platform)
            {
                case PlatformType.Windows:
                    strategy = new WindowsStrategy();
                    break;
                case PlatformType.Android:
                    strategy = new AndoridStrategy();
                    break;
            }
            strategy.Create(filePath, appName);
            strategy.Excute();
            strategy.StartApp();
        }
    }
}
