using System.Diagnostics;
using System.Text;
using GeneralUpdate.ClientCore;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Shared.Object;

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
            
            /*Task.Run(() =>
            {
                //Console.WriteLine("主程序启动辣！！！！");
                //await Task.Delay(3000);
                
                var configinfo = new Configinfo();
                //configinfo.UpdateLogUrl = "https://www.baidu.com";
                configinfo.ReportUrl = "http://127.0.0.1:5008/Upgrade/Report";
                configinfo.UpdateUrl = "http://127.0.0.1:5008/Upgrade/Verification";
                
                configinfo.AppName = "GeneralUpdate.Upgrad.exe";
                configinfo.MainAppName = "GeneralUpdate.Client.exe";
                configinfo.InstallPath = Thread.GetDomain().BaseDirectory;
                configinfo.Bowl = "Generalupdate.CatBowl.exe";
                
                //当前客户端的版本号
                configinfo.ClientVersion = "1.0.0.0";
                //当前升级端的版本号
                configinfo.UpgradeClientVersion = "1.0.0.0";
                
                //平台
                configinfo.Platform = 1;
                //产品id
                configinfo.ProductId = "a77c9df5-45f8-4ee9-b3ad-b9431ce0b51c";
                //应用密钥
                configinfo.AppSecretKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                
                _ = new GeneralClientBootstrap()//单个或多个更新包下载通知事件
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
            });*/
            
            var paramsOSS = new GlobalConfigInfoOSS();
            paramsOSS.Url = "http://192.168.50.203/versions.json";
            paramsOSS.CurrentVersion = "1.0.0.0";
            paramsOSS.VersionFileName = "versions.json";
            paramsOSS.AppName = "GeneralUpdate.Client.exe";
            paramsOSS.Encoding = Encoding.UTF8.WebName;
            GeneralClientOSS.Start(paramsOSS);
            
            Console.Read();
        }

        private static void OnMultiDownloadError(object arg1, MultiDownloadErrorEventArgs arg2)
        {
            Debug.WriteLine(arg2.Exception);
        }

        private static void OnMultiAllDownloadCompleted(object arg1, MultiAllDownloadCompletedEventArgs arg2)
        {
            Debug.WriteLine(arg2.IsAllDownloadCompleted);
        }

        private static void OnMultiDownloadCompleted(object arg1, MultiDownloadCompletedEventArgs arg2)
        {
            var v = arg2.Version;
        }

        private static void OnMultiDownloadStatistics(object arg1, MultiDownloadStatisticsEventArgs arg2)
        {
            Debug.WriteLine(arg2.Speed);
        }

        private static void OnMultiDownloadProgressChanged(object arg1, MultiDownloadProgressChangedEventArgs arg2)
        {
            Debug.WriteLine(arg2.ProgressValue);
        }

        private static void OnException(object arg1, ExceptionEventArgs arg2)
        {
            Debug.WriteLine(arg2.Exception);
        }
    }
}