using GeneralUpdate.ClientCore;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System.Text;

namespace GeneralUpdate.Client
{
    public partial class MainPage : ContentPage
    {
        private const string baseUrl = @"http://127.0.0.1:5001";
        private const string hubName = "versionhub";

        public MainPage()
        {
            InitializeComponent();
            MyButton.Clicked += OnClicked;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            //var md5 = FileUtil.GetFileMD5(@"F:\test\update.zip");
            //VersionHub<string>.Instance.Subscribe($"{baseUrl}/{hubName}", "TESTNAME", new Action<string>(GetMessage));
        }

        private async void GetMessage(string msg)
        {
            var isUpdate = await Shell.Current.DisplayAlert("New Version", "There are new version push messages !", "update", "cancel");
            if (isUpdate) Upgrade();
        }

        private void OnClicked(object sender, EventArgs e) => Upgrade();

        private void Upgrade()
        {
            Task.Run(async () =>
            {
                var url = "http://192.168.50.203";
                var appName = "GeneralUpdate.Client";
                var version = "1.0.0.0";
                var versionFileName = "version.json";
                ParamsOSS @params = new ParamsOSS(url, appName, version, versionFileName);
                await GeneralClientOSS.Start(@params);
            });

            Task.Run(async () =>
            {
                //ClientStrategy该更新策略将完成1.自动升级组件自更新 2.启动更新组件 3.配置好ClientParameter无需再像之前的版本写args数组进程通讯了。
                //generalClientBootstrap.Config(baseUrl, "B8A7FADD-386C-46B0-B283-C9F963420C7C").
                var configinfo = GetWindowsConfiginfo();
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
                .Strategy<WindowsStrategy>()
                //注入一个func让用户决定是否跳过本次更新，如果是强制更新则不生效
                .SetCustomOption(ShowCustomOption)
                //默认黑名单文件： { "Newtonsoft.Json.dll" } 默认黑名单文件扩展名： { ".patch", ".7z", ".zip", ".rar", ".tar" , ".json" }
                //如果不需要扩展，需要重新传入黑名单集合来覆盖。
                .SetBlacklist(GetBlackFiles(), GetBlackFormats())
                .LaunchTaskAsync();
            });
        }

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
        private Configinfo GetWindowsConfiginfo()
        {
            //该对象用于主程序客户端与更新组件进程之间交互用的对象
            var config = new Configinfo();
            //本机的客户端程序应用地址
            config.InstallPath = @"F:\test\Run";
            //更新公告网页
            config.UpdateLogUrl = "https://www.baidu.com/";
            //客户端当前版本号
            config.ClientVersion = "1.1.1.1";
            //客户端类型：1.主程序客户端 2.更新组件
            config.AppType = AppType.ClientApp;
            //指定应用密钥，用于区分客户端应用
            config.AppSecretKey = "B8A7FADD-386C-46B0-B283-C9F963420C7C";
            //更新组件更新包下载地址
            config.UpdateUrl = $"{baseUrl}/versions/{config.AppType}/{config.ClientVersion}/{config.AppSecretKey}";
            //更新程序exe名称
            config.AppName = "AutoUpdate.Core";
            //主程序客户端exe名称
            config.MainAppName = "AutoUpdate.ClientCore";
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
        private Configinfo GetAndroidConfiginfo()
        {
            var config = new Configinfo();
            config.InstallPath = System.Threading.Thread.GetDomain().BaseDirectory;
            //主程序客户端当前版本号
            config.ClientVersion = VersionTracking.Default.CurrentVersion.ToString();
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
            return await Shell.Current.Dispatcher.DispatchAsync(() =>
            {
                return Shell.Current.DisplayAlert("Upgrad Tip", "A discrepancy between the local and server versions has been detected. Do you want to update?", "skip", "update");
            });
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
            MyProgressBar.ProgressTo(e.ProgressValue, 100, Easing.Default);
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
            Shell.Current.Dispatcher.DispatchAsync(() =>
            {
                MyLabel.Text = message;
            });
        }
    }
}