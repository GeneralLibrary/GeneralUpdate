using System.Text;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Driver;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine($"升级程序初始化，{DateTime.Now}！");
                Console.WriteLine("当前运行目录：" + Thread.GetDomain().BaseDirectory);
                await Task.Delay(2000);
                await new GeneralUpdateBootstrap()
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
                    //设置字段映射表，用于解析所有驱动包的信息的字符串
                    //.SetFieldMappings(fieldMappingsCN)
                    //是否开启驱动更新
                    //.Option(UpdateOption.Drive, true)
                    .LaunchAsync();
                Console.WriteLine($"升级程序已启动，{DateTime.Now}！");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
            }
            
            //中文操作系统的驱动包字段映射表，用于解析所有驱动包的信息的字符串
            /*var fieldMappingsCN = new Dictionary<string, string>
            {
                { "PublishedName", "发布名称" },
                { "OriginalName", "原始名称" },
                { "Provider", "提供程序名称" },
                { "ClassName", "类名" },
                { "ClassGUID", "类 GUID" },
                { "Version", "驱动程序版本" },
                { "Signer", "签名者姓名" }
            };

            //英文操作系统的驱动包字段映射表，用于解析所有驱动包的信息的字符串
            var fieldMappingsEN = new Dictionary<string, string>
            {
                { "PublishedName", "Driver" },
                { "OriginalName", "OriginalFileName" },
                { "Provider", "ProviderName" },
                { "ClassName", "ClassName" },
                { "Version", "Version" }
            };

            var fileExtension = ".inf";
            var outPutPath = @"D:\drivers\";
            var driversPath = @"D:\driverslocal\";

            var information = new DriverInformation.Builder()
                .SetDriverFileExtension(fileExtension)
                .SetOutPutDirectory(outPutPath)
                .SetDriverDirectory(driversPath)
                .SetFieldMappings(fieldMappingsCN)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();*/

            //GeneralUpdateOSS.Start();
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
            Console.WriteLine(arg2.IsAllDownloadCompleted ? "所有的下载任务已完成！" : $"下载任务已失败！{arg2.FailedVersions.Count}");
        }

        private static void OnMultiDownloadCompleted(object arg1, MultiDownloadCompletedEventArgs arg2)
        {
            var version = arg2.Version as VersionInfo;
            Console.WriteLine(arg2.IsComplated ? $"当前下载版本：{version.Version}, 下载完成！" : $"当前下载版本：{version.Version}, 下载失败！");
        }

        private static void OnMultiDownloadStatistics(object arg1, MultiDownloadStatisticsEventArgs arg2)
        {
            var version = arg2.Version as VersionInfo;
            Console.WriteLine($"当前下载版本：{version.Version}，下载速度：{arg2.Speed}，剩余下载时间：{arg2.Remaining}，已下载大小：{arg2.BytesReceived}，总大小：{arg2.TotalBytesToReceive}, 进度百分比：{arg2.ProgressPercentage}%");
        }

        private static void OnException(object arg1, ExceptionEventArgs arg2)
        {
            Console.WriteLine($"{arg2.Exception}");
        }
    }
}