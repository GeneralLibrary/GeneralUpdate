using GeneralUpdate.ClientCore;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Differential;
using System.IO;
using GeneralUpdate.Core.Driver;
using System.Diagnostics;
using GeneralUpdate.Core.WillMessage;
using GeneralUpdate.Core.ContentProvider;

namespace GeneralUpdate.Client
{
    internal class MySample
    {
        #region 推送功能

        private const string baseUrl = @"http://127.0.0.1:5000";
        private const string hubName = "versionhub";

        internal MySample()
        {
            //Receive sample code pushed by the server
            //VersionHub<string>.Instance.Subscribe($"{baseUrl}/{hubName}", "TESTNAME", new Action<string>(GetMessage));
        }

        //Receive sample code pushed by the server
        private async void GetMessage(string msg)
        {
            var isUpdate = true;
            if (isUpdate) Upgrade();
        }

        #endregion

        #region 常规更新

        public async Task Upgrade()
        {
            //Task.Run(async () =>
            //{
            //    var url = "http://192.168.50.203";
            //    var appName = "GeneralUpdate.Client";
            //    var version = "1.0.0.0";
            //    var versionFileName = "version.json";
            //    ParamsOSS @params = new ParamsOSS(url, appName, version, versionFileName);
            //    await GeneralClientOSS.Start(@params);
            //});

            //ClientStrategy该更新策略将完成1.自动升级组件自更新 2.启动更新组件 3.配置好ClientParameter无需再像之前的版本写args数组进程通讯了。
            //generalClientBootstrap.Config(baseUrl, "B8A7FADD-386C-46B0-B283-C9F963420C7C").
            var configinfo = GetWindowsConfigInfo();
            var generalClientBootstrap = await new GeneralClientBootstrap()
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
            .Config(configinfo)
            .Option(UpdateOption.DownloadTimeOut, 60)
            .Option(UpdateOption.Encoding, Encoding.Default)
            .Option(UpdateOption.Format, Format.ZIP)
            //开启驱动更新
            //.Option(UpdateOption.Drive, true)
            //开启遗言功能，需要部署GeneralUpdate.SystemService Windows服务。
            .Option(UpdateOption.WillMessage, true)
            .Strategy<WindowsStrategy>()
            //注入一个func让用户决定是否跳过本次更新，如果是强制更新则不生效
            //.SetCustomSkipOption(ShowCustomOption)
            //注入一个自定义方法集合，该集合会在更新启动前执行。执行自定义方法列表如果出现任何异常，将通过异常订阅通知。（推荐在更新之前检查当前软件环境）
            //.AddCustomOption(new List<Func<bool>>() { () => Check1(), () => Check2() })
            //默认黑名单文件： { "Newtonsoft.Json.dll" } 默认黑名单文件扩展名： { ".patch", ".7z", ".zip", ".rar", ".tar" , ".json" }
            //如果不需要扩展，需要重新传入黑名单集合来覆盖。
            //.SetBlacklist(GetBlackFiles(), GetBlackFormats())
            .LaunchTaskAsync();
        }

        private bool Check1() => true;

        private bool Check2() => true;

        private List<string> GetBlackFiles()
        {
            var blackFiles = new List<string>();
            blackFiles.Add("MainApp");
            return blackFiles;
        }

        private List<string> GetBlackFormats()
        {
            var blackFormats = new List<string>();
            blackFormats.Add(".zip");
            return blackFormats;
        }

        /// <summary>
        /// 获取Windows平台所需的配置参数
        /// </summary>
        /// <returns></returns>
        private Configinfo GetWindowsConfigInfo()
        {
            //该对象用于主程序客户端与更新组件进程之间交互用的对象
            var config = new Configinfo();
            //本机的客户端程序应用地址
            config.InstallPath = @"D:\packet\source";
            //更新公告网页
            config.UpdateLogUrl = "https://www.baidu.com/";
            //客户端当前版本号
            config.ClientVersion = "1.1.1.1";
            //客户端类型：1.主程序客户端 2.更新组件
            config.AppType = AppType.UpgradeApp;
            //指定应用密钥，用于区分客户端应用
            config.AppSecretKey = "B8A7FADD-386C-46B0-B283-C9F963420C7C";
            //更新组件更新包下载地址
            config.UpdateUrl = $"{baseUrl}/versions/{config.AppType}/{config.ClientVersion}/{config.AppSecretKey}";
            //更新程序exe名称
            config.AppName = "GeneralUpdate.Core";
            //主程序客户端exe名称
            config.MainAppName = "GeneralUpdate.ClientCore";
            //主程序信息
            var mainVersion = "1.1.1.1";
            //主程序客户端更新包下载地址
            config.MainUpdateUrl = $"{baseUrl}/versions/{AppType.ClientApp}/{mainVersion}/{config.AppSecretKey}";
            return config;
        }

        /// <summary>
        /// 获取Android平台所需要的参数
        /// </summary>
        /// <returns></returns>
        private Configinfo GetAndroidConfigInfo()
        {
            var config = new Configinfo();
            config.InstallPath = System.Threading.Thread.GetDomain().BaseDirectory;
            //主程序客户端当前版本号
            config.ClientVersion = "1.0.0.0"; //VersionTracking.Default.CurrentVersion.ToString();
            config.AppType = AppType.ClientApp;
            config.AppSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
            //主程序客户端exe名称
            config.MainAppName = "GeneralUpdate.ClientCore";
            //主程序信息
            var mainVersion = "1.1.1.1";
            config.MainUpdateUrl = $"{baseUrl}/versions/{AppType.ClientApp}/{mainVersion}/{config.AppSecretKey}";
            return config;
        }

        /// <summary>
        /// 让用户决定是否跳过本次更新
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ShowCustomOption()
        {
            return await Task.FromResult(true);
        }

        private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        {
            //e.Remaining 剩余下载时间
            //e.Speed 下载速度
            //e.Version 当前下载的版本信息
        }

        private void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        {
            //e.TotalBytesToReceive 当前更新包需要下载的总大小
            //e.ProgressValue 当前进度值
            //e.ProgressPercentage 当前进度的百分比
            //e.Version 当前下载的版本信息
            //e.Type 当前正在执行的操作  1.ProgressType.Check 检查版本信息中 2.ProgressType.Donwload 正在下载当前版本 3. ProgressType.Updatefile 更新当前版本 4. ProgressType.Done更新完成 5.ProgressType.Fail 更新失败
            //e.BytesReceived 已下载大小
            DispatchMessage($"{e.ProgressPercentage}%");
            //MyProgressBar.ProgressTo(e.ProgressValue, 100, Easing.Default);
        }

        private void OnException(object sender, ExceptionEventArgs e)
        {
            //DispatchMessage(e.Exception.Message);
        }

        private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        {
            //e.FailedVersions; 如果出现下载失败则会把下载错误的版本、错误原因统计到该集合当中。
            DispatchMessage($"Is all download completed {e.IsAllDownloadCompleted}.");
        }

        private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        {
            var info = e.Version as VersionInfo;
            DispatchMessage($"{info.Name} download completed.");
        }

        private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            var info = e.Version as VersionInfo;
            DispatchMessage($"{info.Name} error!");
        }

        private void DispatchMessage(string message)
        {

        }

        #endregion

        #region 测试二进制更新包整理

        public async Task TestDifferentialClean() 
        {
            var path1 = "D:\\packet\\source";
            var path2 = "D:\\packet\\target";
            var path3 = "D:\\packet\\patchs";
            await DifferentialCore.Instance.Clean(path1, path2, path3);
        }

        public async Task TestDifferentialDirty() 
        {
            var path1 = "D:\\packet\\source";
            var path2 = "D:\\packet\\patchs";
            await DifferentialCore.Instance.Dirty(path1, path2);
        }

        #endregion

        #region 测试驱动功能

        public void TestDrive()
        {
            var path1 = "D:\\packet\\source";
            var path2 = "D:\\packet\\target";

            var drivers = GetAllDriverDirectories(path1);

            var information = new DriverInformation.Builder()
                .SetInstallDirectory(path1)
                .SetOutPutDirectory(path2)
                .SetDriverNames(drivers)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();
        }

        /// <summary>
        /// Identifies all folders containing driver files in the specified directory and returns the directory collection.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private List<string> GetAllDriverDirectories(string path)
        {
            var driverDirectories = new HashSet<string>();
            try
            {
                foreach (string filePath in Directory.GetFiles(path))
                {
                    if (IsDriverFile(filePath))
                        driverDirectories.Add(filePath);
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    driverDirectories.UnionWith(GetAllDriverDirectories(directory));
                }
            }
            catch (UnauthorizedAccessException)
            {
                Trace.WriteLine("No access directory：" + path);
            }
            catch (PathTooLongException)
            {
                Trace.WriteLine("Path overlength：" + path);
            }

            return new List<string>(driverDirectories);
        }

        /// <summary>
        /// Match the driver installation boot file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool IsDriverFile(string filePath) =>
            string.Equals(Path.GetExtension(filePath), ".inf", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region 测试WillMessage

        public void TestWillMessage()
        {
            var path1 = "D:\\packet\\source";
            var path2 = "D:\\packet\\target";
            var hash = "28d10f1fc2a23dd1afe0af40d132b25c72ea56005963f653c27889f03d381c8d";

            for (int i = 0; i < 1; i++)
            {
                var version = "1.0.0." + i;
                WillMessageManager.Instance.Backup(path1,path2, version, hash, 1);
            }
            WillMessageManager.Instance.Builder();
            var obj = WillMessageManager.Instance.GetWillMessage();
            WillMessageManager.Instance.Check();
            WillMessageManager.Instance.Restore();
            //WillMessageManager.Instance.Clear();
        }

        #endregion

        #region 文件管理测试

        public void TestFileProvider() 
        {
            //FileProvider fileProvider = new FileProvider();
            //fileProvider.Handle();
        }

        #endregion
    }
}
