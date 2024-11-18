using System.Diagnostics;
using System.Text;
using GeneralUpdate.ClientCore;
using GeneralUpdate.ClientCore.Hubs;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;

namespace GeneralUpdate.Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            /*Task.Run(async () =>
            {
                var source = @"D:\packet\app";
                var target = @"D:\packet\release";
                var patch = @"D:\packet\patch";

                await DifferentialCore.Instance?.Clean(source, target, patch);
                await DifferentialCore.Instance?.Dirty(source, patch);
            });*/
            
            Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("主程序启动辣！！！！");
                    await Task.Delay(3000);

                    var configinfo = new Configinfo
                    {
                        //configinfo.UpdateLogUrl = "https://www.baidu.com";
                        ReportUrl = "http://127.0.0.1:5000/Upgrade/Report",
                        UpdateUrl = "http://127.0.0.1:5000/Upgrade/Verification",
                        AppName = "GeneralUpdate.Upgrad.exe",
                        MainAppName = "GeneralUpdate.Client.exe",
                        InstallPath = @"D:\迅雷下载\Client", //Thread.GetDomain().BaseDirectory,
                        //configinfo.Bowl = "Generalupdate.CatBowl.exe";
                        //当前客户端的版本号
                        ClientVersion = "1.0.0.0",
                        //当前升级端的版本号
                        UpgradeClientVersion = "1.0.0.0",
                        //平台
                        Platform = PlatformType.Windwos,
                        //产品id
                        ProductId = "a77c9df5-45f8-4ee9-b3ad-b9431ce0b51c",
                        //应用密钥
                        AppSecretKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
                    };

                    _ = new GeneralClientBootstrap() //单个或多个更新包下载通知事件
                        .AddListenerMultiDownloadProgress(OnMultiDownloadProgressChanged)
                        //单个或多个更新包下载速度、剩余下载事件、当前下载版本信息通知事件
                        .AddListenerMultiDownloadStatistics(OnMultiDownloadStatistics)
                        //单个或多个更新包下载完成
                        .AddListenerMultiDownloadCompleted(OnMultiDownloadCompleted)
                        //完成所有的下载任务通知
                        .AddListenerMultiAllDownloadCompleted(OnMultiAllDownloadCompleted)
                        //下载过程出现的异常通知
                        .AddListenerMultiDownloadError(OnMultiDownloadError)
                        //整个更新过程出现的任何问题都会通过这个事件通知
                        .AddListenerException(OnException)
                        .SetConfig(configinfo)
                        .Option(UpdateOption.DownloadTimeOut, 60)
                        .Option(UpdateOption.Encoding, Encoding.UTF8)
                        .Option(UpdateOption.Format, Format.ZIP)
                        .LaunchAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\n" + e.StackTrace);
                }
            });
            
            /*var paramsOSS = new GlobalConfigInfoOSS();
            paramsOSS.Url = "http://192.168.50.203/versions.json";
            paramsOSS.CurrentVersion = "1.0.0.0";
            paramsOSS.VersionFileName = "versions.json";
            paramsOSS.AppName = "GeneralUpdate.Client.exe";
            paramsOSS.Encoding = Encoding.UTF8.WebName;
            GeneralClientOSS.Start(paramsOSS);*/
            

            /*IUpgradeHubService hub = new UpgradeHubService("http://localhost:5008/UpgradeHub", null, "GeneralUpdate");
            hub.AddListenerReceive(Receive);
            hub.StartAsync().Wait();*/

            while (true)
            {
                var input = Console.ReadLine();
                if (input == "exit")
                {
                    break;
                }
            }
        }

        private static void OnMultiDownloadError(object arg1, MultiDownloadErrorEventArgs arg2)
        {
            var version = arg2.Version as VersionInfo;
            Console.WriteLine($"{version.Version} {arg2.Exception}");
        }

        private static void OnMultiAllDownloadCompleted(object arg1, MultiAllDownloadCompletedEventArgs arg2)
        {
            Console.WriteLine(arg2.IsAllDownloadCompleted);
        }

        private static void OnMultiDownloadCompleted(object arg1, MultiDownloadCompletedEventArgs arg2)
        {
            Console.WriteLine(arg2.Error.ToString());
        }

        private static void OnMultiDownloadStatistics(object arg1, MultiDownloadStatisticsEventArgs arg2)
        {
            Console.WriteLine($"{arg2.Speed}, {arg2.Remaining}");
        }

        private static void OnMultiDownloadProgressChanged(object arg1, MultiDownloadProgressChangedEventArgs arg2)
        {
            Console.WriteLine($"{arg2.TotalBytesToReceive}, {arg2.ProgressValue}");
        }

        private static void OnException(object arg1, ExceptionEventArgs arg2)
        {
            Console.WriteLine($"{arg2.Exception}");
        }
    }
}