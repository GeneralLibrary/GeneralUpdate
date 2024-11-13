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
        private static void Main(string[] args)
        {
            //中文操作系统的驱动包字段映射表，用于解析所有驱动包的信息的字符串
            var fieldMappingsCN = new Dictionary<string, string>
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
            
            //var fileExtension = ".inf";
            //var outPutPath = @"D:\drivers\";
            //var driversPath = @"D:\driverslocal\";

            /*var information = new DriverInformation.Builder()
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

            Task.Run(() =>
            {
                var jsonPath = @"D:\packet\test.json";
                var json = File.ReadAllText(jsonPath);
                Environment.SetEnvironmentVariable("ProcessInfo", json, EnvironmentVariableTarget.User);
                
                _ = new GeneralUpdateBootstrap() //单个或多个更新包下载通知事件
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
                    //设置字段映射表，用于解析所有驱动包的信息的字符串
                    //.SetFieldMappings(fieldMappingsCN)
                    //是否开启驱动更新
                    //.Option(UpdateOption.Drive, true)
                    .LaunchAsync();
            });

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