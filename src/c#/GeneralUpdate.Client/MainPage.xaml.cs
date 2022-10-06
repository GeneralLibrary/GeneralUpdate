using GeneralUpdate.ClientCore;
using GeneralUpdate.ClientCore.Hubs;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys.PlatformAndroid;
using GeneralUpdate.Core.Strategys.PlatformiOS;
using GeneralUpdate.Core.Strategys.PlatformMac;
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
            VersionHub<string>.Instance.Subscribe($"{baseUrl}/{hubName}", "TESTNAME", new Action<string>(GetMessage));
        }

        private async void GetMessage(string msg)
        {
            var isUpdate = await Shell.Current.DisplayAlert("New Version", "There are new version push messages !", "update","cancel");
            if (isUpdate) Upgrade();
        }

        private void OnClicked(object sender, EventArgs e)=> Upgrade();

        private void Upgrade() 
        {
            Task.Run(async () =>
            {
                //主程序信息
                var mainVersion = "1.1.1.1";

                #region update app.

                //该对象用于主程序客户端与更新组件进程之间交互用的对象
                //clientParameter = new ClientParameter();

                //本机的客户端程序应用地址
                //clientParameter.InstallPath = @"D:\Updatetest_hub\Run_app";
                //更新公告网页
                //clientParameter.UpdateLogUrl = "https://www.baidu.com/";

                //clientParameter.ClientVersion = "9.1.3.0";//"1.1.1.1";

                ////客户端类型：1.主程序客户端 2.更新组件
                //clientParameter.AppType = (int)AppType.UpdateApp;
                //clientParameter.AppSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
                ////更新组件请求验证更新的服务端地址
                //clientParameter.ValidateUrl = $"{baseUrl}/validate/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
                ////更新组件更新包下载地址
                //clientParameter.UpdateUrl = $"{baseUrl}/versions/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
                ////更新程序exe名称
                //clientParameter.AppName = "AutoUpdate.Core";
                //指定应用密钥，用于区分客户端应用

                #endregion update app.

                #region main app.

                //更新组件的版本号
                //clientParameter.ClientVersion = "1.1.1";
                //主程序客户端exe名称
                //clientParameter.MainAppName = "AutoUpdate.ClientCore";
                //主程序客户端请求验证更新的服务端地址
                //clientParameter.MainValidateUrl = $"{baseUrl}/validate/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";
                //主程序客户端更新包下载地址
                //clientParameter.MainUpdateUrl = $"{baseUrl}/versions/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";

                #endregion main app.

                var generalClientBootstrap = new GeneralClientBootstrap();
                //单个或多个更新包下载通知事件
                generalClientBootstrap.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                //单个或多个更新包下载速度、剩余下载事件、当前下载版本信息通知事件
                generalClientBootstrap.MutiDownloadStatistics += OnMutiDownloadStatistics;
                //单个或多个更新包下载完成
                generalClientBootstrap.MutiDownloadCompleted += OnMutiDownloadCompleted;
                //完成所有的下载任务通知
                generalClientBootstrap.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                //下载过程出现的异常通知
                generalClientBootstrap.MutiDownloadError += OnMutiDownloadError;
                //整个更新过程出现的任何问题都会通过这个事件通知
                generalClientBootstrap.Exception += OnException;
                //ClientStrategy该更新策略将完成1.自动升级组件自更新 2.启动更新组件 3.配置好ClientParameter无需再像之前的版本写args数组进程通讯了。
                generalClientBootstrap.Config(baseUrl, "B8A7FADD-386C-46B0-B283-C9F963420C7C").
                Option(UpdateOption.DownloadTimeOut, 60).
                Option(UpdateOption.Encoding, Encoding.Default).
                Option(UpdateOption.Format, Format.ZIP).
                //注入一个func让用户决定是否跳过本次更新，如果是强制更新则不生效
                SetCustomOption(ShowCustomOption).
                Strategy<WindowsStrategy>();
                await generalClientBootstrap.LaunchTaskAsync();
            });
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

        private void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        {
            //e.Remaining 剩余下载时间
            //e.Speed 下载速度
            //e.Version 当前下载的版本信息
        }

        private void OnMutiDownloadProgressChanged(object sender, MutiDownloadProgressChangedEventArgs e)
        {
            //e.TotalBytesToReceive 当前更新包需要下载的总大小
            //e.ProgressValue 当前进度值
            //e.ProgressPercentage 当前进度的百分比
            //e.Version 当前下载的版本信息
            //e.Type 当前正在执行的操作  1.ProgressType.Check 检查版本信息中 2.ProgressType.Donwload 正在下载当前版本 3. ProgressType.Updatefile 更新当前版本 4. ProgressType.Done更新完成 5.ProgressType.Fail 更新失败
            //e.BytesReceived 已下载大小
            DispatchMessage($"{e.ProgressPercentage}%");
            MyProgressBar.ProgressTo(e.ProgressValue,100,Easing.Default);
        }

        private void OnException(object sender, ExceptionEventArgs e)
        {
            DispatchMessage(e.Exception.Message);
        }

        private void OnMutiAllDownloadCompleted(object sender, MutiAllDownloadCompletedEventArgs e)
        {
            //e.FailedVersions; 如果出现下载失败则会把下载错误的版本、错误原因统计到该集合当中。
            DispatchMessage($"Is all download completed {e.IsAllDownloadCompleted}.");
        }

        private void OnMutiDownloadCompleted(object sender, MutiDownloadCompletedEventArgs e)
        {
            var info = e.Version as VersionInfo;
            DispatchMessage($"{info.Name} download completed.");
        }

        private void OnMutiDownloadError(object sender, MutiDownloadErrorEventArgs e)
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