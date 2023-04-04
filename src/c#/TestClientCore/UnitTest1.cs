namespace TestClientCore
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        //public delegate void BoilHandle(object obj, EventArgs eventArgs);
        //public event BoilHandle BoilEvent;//封装了委托

        //public void Test1()
        //{
        //    BoilEvent += Program_BoilEvent;
        //    GeneralEventManager.Instance.AddListener<BoilHandle>(BoilEvent);
        //    GeneralEventManager.Instance.Dispatch<BoilHandle>(this, new EventArgs());
        //}

        //private void Program_BoilEvent(object obj, EventArgs eventArgs)
        //{
        //    Console.WriteLine(1);
        //}

        //private const string baseUrl = @"http://127.0.0.1:5001", hubName = "versionhub";
        //private const string baseUrl1 = @"http://127.0.0.1:5001";
        //private const string baseUrl2 = @"http://127.0.0.1:5001";
        ////InitVersionHub();
        //#region VersionHub

        ///// <summary>
        ///// Subscription server push version message.
        ///// </summary>
        //private void InitVersionHub()
        //{
        //    VersionHub<string>.Instance.Subscribe($"{baseUrl}/{hubName}", "TESTNAME", new Action<string>(GetMessage));
        //}

        //private void GetMessage(string msg)
        //{
        //    TxtMessage.Text = msg;
        //}

        //#endregion VersionHub

        //#region GeneralUpdate Core

        ////private ClientParameter clientParameter;

        //private void BtnClientTest_Click(object sender, RoutedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        //主程序信息
        //        var mainVersion = "1.1.1.1";

        //        //该对象用于主程序客户端与更新组件进程之间交互用的对象
        //        //clientParameter = new ClientParameter();

        //        //本机的客户端程序应用地址
        //        //clientParameter.InstallPath = @"D:\Updatetest_hub\Run_app";
        //        //更新公告网页
        //        //clientParameter.UpdateLogUrl = "https://www.baidu.com/";

        //        #region update app.

        //        //clientParameter.ClientVersion = "9.1.3.0";//"1.1.1.1";

        //        ////客户端类型：1.主程序客户端 2.更新组件
        //        //clientParameter.AppType = (int)AppType.UpdateApp;
        //        //clientParameter.AppSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
        //        ////更新组件请求验证更新的服务端地址
        //        //clientParameter.ValidateUrl = $"{baseUrl}/validate/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
        //        ////更新组件更新包下载地址
        //        //clientParameter.UpdateUrl = $"{baseUrl}/versions/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
        //        ////更新程序exe名称
        //        //clientParameter.AppName = "AutoUpdate.Core";
        //        //指定应用密钥，用于区分客户端应用

        //        #endregion update app.

        //        #region main app.

        //        //更新组件的版本号
        //        //clientParameter.ClientVersion = "1.1.1";
        //        //主程序客户端exe名称
        //        //clientParameter.MainAppName = "AutoUpdate.ClientCore";
        //        //主程序客户端请求验证更新的服务端地址
        //        //clientParameter.MainValidateUrl = $"{baseUrl}/validate/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";
        //        //主程序客户端更新包下载地址
        //        //clientParameter.MainUpdateUrl = $"{baseUrl}/versions/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";

        //        #endregion main app.

        //        var generalClientBootstrap = new GeneralClientBootstrap();
        //        //单个或多个更新包下载通知事件
        //        generalClientBootstrap.MultiDownloadProgressChanged += OnMultiDownloadProgressChanged;
        //        //单个或多个更新包下载速度、剩余下载事件、当前下载版本信息通知事件
        //        generalClientBootstrap.MultiDownloadStatistics += OnMultiDownloadStatistics;
        //        //单个或多个更新包下载完成
        //        generalClientBootstrap.MultiDownloadCompleted += OnMultiDownloadCompleted;
        //        //完成所有的下载任务通知
        //        generalClientBootstrap.MultiAllDownloadCompleted += OnMultiAllDownloadCompleted;
        //        //下载过程出现的异常通知
        //        generalClientBootstrap.MultiDownloadError += OnMultiDownloadError;
        //        //整个更新过程出现的任何问题都会通过这个事件通知
        //        generalClientBootstrap.Exception += OnException;
        //        //ClientStrategy该更新策略将完成1.自动升级组件自更新 2.启动更新组件 3.配置好ClientParameter无需再像之前的版本写args数组进程通讯了。
        //        generalClientBootstrap.Config("", "").
        //        //generalClientBootstrap.Config(baseUrl,"appsecretkey").
        //        Option(UpdateOption.DownloadTimeOut, 60).
        //        Option(UpdateOption.Encoding, Encoding.Default).
        //        Option(UpdateOption.Format, "zip").
        //        //注入一个func让用户决定是否跳过本次更新，如果是强制更新则不生效
        //        SetCustomOption(ShowCustomOption).
        //        Strategy<ClientStrategy>();
        //        await generalClientBootstrap.LaunchTaskAsync();
        //    });
        //}

        ///// <summary>
        ///// 让用户决定是否跳过本次更新
        ///// </summary>
        ///// <returns></returns>
        //private bool ShowCustomOption()
        //{
        //    var messageBoxResult = MessageBox.Show("检测到本地与服务器版本不一致，是否更新？", "click", MessageBoxButton.YesNoCancel);
        //    return messageBoxResult == MessageBoxResult.Yes;
        //}

        //private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        //{
        //    //e.Remaining 剩余下载时间
        //    //e.Speed 下载速度
        //    //e.Version 当前下载的版本信息
        //}

        //private void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        //{
        //    //e.TotalBytesToReceive 当前更新包需要下载的总大小
        //    //e.ProgressValue 当前进度值
        //    //e.ProgressPercentage 当前进度的百分比
        //    //e.Version 当前下载的版本信息
        //    //e.Type 当前正在执行的操作  1.ProgressType.Check 检查版本信息中 2.ProgressType.Donwload 正在下载当前版本 3. ProgressType.Updatefile 更新当前版本 4. ProgressType.Done更新完成 5.ProgressType.Fail 更新失败
        //    //e.BytesReceived 已下载大小
        //}

        //private void OnException(object sender, ExceptionEventArgs e)
        //{
        //    Debug.WriteLine(e.Exception.Message);
        //}

        //private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        //{
        //    //e.FailedVersions; 如果出现下载失败则会把下载错误的版本、错误原因统计到该集合当中。
        //    Debug.WriteLine($"Is all download completed {e.IsAllDownloadCompleted}.");
        //}

        //private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        //{
        //    //Debug.WriteLine($"{ e.Version.Name } download completed.");
        //}

        //private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        //{
        //    //Debug.WriteLine($"{ e.Version.Name } error!");
        //}

        //#endregion GeneralUpdate Core
    }
}