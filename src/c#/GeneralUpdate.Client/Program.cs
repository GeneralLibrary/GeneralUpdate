using System.Text;
using GeneralUpdate.ClientCore;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Client
{
    internal class Progra
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
                var configinfo = new Configinfo();
                configinfo.UpdateLogUrl = "https://www.baidu.com";
                configinfo.ReportUrl = "http://127.0.0.1:5008/Upgrade/Report";
                configinfo.UpdateUrl = "http://127.0.0.1:5008/Upgrade/Verification";
                
                configinfo.AppName = "GeneralUpdate.Upgrade";
                configinfo.MainAppName = "GeneralUpdate.Client";
                configinfo.InstallPath = Thread.GetDomain().BaseDirectory;
                
                configinfo.ClientVersion = "1.0.0.0";
                configinfo.UpgradeClientVersion = "1.0.0.0";
                
                configinfo.Platform = 1;
                configinfo.ProductId = "9999";
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
                    .Option(UpdateOption.Encoding, Encoding.Default)
                    .Option(UpdateOption.Format, Format.ZIP)
                    .Option(UpdateOption.Drive, false)
                    .LaunchAsync();
            });*/

            Console.Read();
        }

        private static void OnMultiDownloadError(object arg1, MultiDownloadErrorEventArgs arg2)
        {
        }

        private static void OnMultiAllDownloadCompleted(object arg1, MultiAllDownloadCompletedEventArgs arg2)
        {
        }

        private static void OnMultiDownloadCompleted(object arg1, MultiDownloadCompletedEventArgs arg2)
        {
        }

        private static void OnMultiDownloadStatistics(object arg1, MultiDownloadStatisticsEventArgs arg2)
        {
        }

        private static void OnMultiDownloadProgressChanged(object arg1, MultiDownloadProgressChangedEventArgs arg2)
        {
        }

        private static void OnException(object arg1, ExceptionEventArgs arg2)
        {
        }
    }
}