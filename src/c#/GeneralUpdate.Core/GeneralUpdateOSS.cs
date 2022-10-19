using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.OSS;
using GeneralUpdate.Core.OSS.Strategys;
using GeneralUpdate.Core.OSS.Strategys.PlatformWindows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Core
{
    public sealed class GeneralUpdateOSS
    {
        //public static async Task Start<T>(string url,string platform = PlatformType.Windows) where T : IOSS, new()
        //{
        //    IStrategy strategy = null;
        //    string targetPath = "";
        //    var oss = new T();
        //    oss.SetParmeter(url, targetPath);
        //    await oss.Download();
        //    if (File.Exists(targetPath)) throw new Exception($"The file was not found : {targetPath}!");
        //    string configContent = File.ReadAllText(targetPath);
        //    var configDO = JsonConvert.DeserializeObject<List<VersionConfigDO>>(configContent);
        //    foreach (var config in configDO)
        //    {
        //        oss.SetParmeter(config.Url, targetPath);
        //        await oss.Download();
        //    }
        //    switch (platform)
        //    {
        //        case PlatformType.Windows:
        //            strategy = new WindowsStrategy();
        //            break;
        //        case PlatformType.Linux:
        //            break;
        //        case PlatformType.Mac:
        //            break;
        //        case PlatformType.Android:
        //            break;
        //        case PlatformType.iOS:
        //            break;
        //    }
        //    strategy.Excute();
        //    strategy.StartApp("");
        //}
    }
}
