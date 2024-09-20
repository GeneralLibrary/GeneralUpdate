using GeneralUpdate.Core;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System.Text;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                //var url = "http://192.168.50.203";
                //var appName = "GeneralUpdate.Client";
                //var version = "1.0.0.0";

                ////Part1 OSS服务示例
                //var versionFileName = "version.json";
                //ParamsOSS @params = new ParamsOSS(url, appName, version, versionFileName);
                //GeneralUpdateOSS.AddListenerMultiDownloadProgress(OnMultiDownloadProgressChanged);
                ////单个或多个更新包下载速度、剩余下载事件、当前下载版本信息通知事件
                //GeneralUpdateOSS.AddListenerMultiDownloadStatistics(OnMultiDownloadStatistics);
                ////单个或多个更新包下载完成
                //GeneralUpdateOSS.AddListenerMultiDownloadCompleted(OnMultiDownloadCompleted);
                ////完成所有的下载任务通知
                //GeneralUpdateOSS.AddListenerMultiAllDownloadCompleted(OnMultiAllDownloadCompleted);
                ////下载过程出现的异常通知
                //GeneralUpdateOSS.AddListenerMultiDownloadError(OnMultiDownloadError);
                ////整个更新过程出现的任何问题都会通过这个事件通知
                //GeneralUpdateOSS.AddListenerException(OnException);
                //await GeneralUpdateOSS.Start<OSSStrategy>(@params,Encoding.Default);

                //Part 2 常规更新示例
                var bootStrap = await new GeneralUpdateBootstrap()
                //单个或多个更新包下载通知事件
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
                .Strategy<WindowsStrategy>()
                //.Option(UpdateOption.Encoding, Encoding.Default)
                //.Option(UpdateOption.DownloadTimeOut, 60)
                //.Option(UpdateOption.Format, Format.ZIP)
                .LaunchTaskAsync();
            });
            Console.Read();
        }

        private static void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        {
            Console.WriteLine($" {e.Speed} , {e.Remaining.ToShortTimeString()}");
        }

        private static void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        {
            switch (e.Type)
            {
                case ProgressType.Check:
                    break;

                case ProgressType.Download:
                    Console.WriteLine($" {Math.Round(e.ProgressValue * 100, 2)}% ， Receivedbyte：{e.BytesReceived}M ，Totalbyte：{e.TotalBytesToReceive}M");
                    break;

                case ProgressType.Updatefile:
                    break;

                case ProgressType.Done:
                    break;

                case ProgressType.Fail:
                    break;
            }
        }

        private static void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        {
            //var info = e.Version as GeneralUpdate.Core.Domain.Entity.VersionInfo;
            //Console.WriteLine($"{info.Name} download completed.");
        }

        private static void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        {
            Console.WriteLine($"AllDownloadCompleted {e.IsAllDownloadCompleted}");
        }

        private static void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            //var info = e.Version as GeneralUpdate.Core.Domain.Entity.VersionInfo;
            //Console.WriteLine($"{info.Name},{e.Exception.Message}.");
        }

        private static void OnException(object sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"{e.Exception.Message}");
        }
    }
}