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
        //public event BoilHandle BoilEvent;//��װ��ί��

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
        //        //��������Ϣ
        //        var mainVersion = "1.1.1.1";

        //        //�ö�������������ͻ���������������֮�佻���õĶ���
        //        //clientParameter = new ClientParameter();

        //        //�����Ŀͻ��˳���Ӧ�õ�ַ
        //        //clientParameter.InstallPath = @"D:\Updatetest_hub\Run_app";
        //        //���¹�����ҳ
        //        //clientParameter.UpdateLogUrl = "https://www.baidu.com/";

        //        #region update app.

        //        //clientParameter.ClientVersion = "9.1.3.0";//"1.1.1.1";

        //        ////�ͻ������ͣ�1.������ͻ��� 2.�������
        //        //clientParameter.AppType = (int)AppType.UpdateApp;
        //        //clientParameter.AppSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
        //        ////�������������֤���µķ���˵�ַ
        //        //clientParameter.ValidateUrl = $"{baseUrl}/validate/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
        //        ////����������°����ص�ַ
        //        //clientParameter.UpdateUrl = $"{baseUrl}/versions/{ clientParameter.AppType }/{ clientParameter.ClientVersion }/{clientParameter.AppSecretKey}";
        //        ////���³���exe����
        //        //clientParameter.AppName = "AutoUpdate.Core";
        //        //ָ��Ӧ����Կ���������ֿͻ���Ӧ��

        //        #endregion update app.

        //        #region main app.

        //        //��������İ汾��
        //        //clientParameter.ClientVersion = "1.1.1";
        //        //������ͻ���exe����
        //        //clientParameter.MainAppName = "AutoUpdate.ClientCore";
        //        //������ͻ���������֤���µķ���˵�ַ
        //        //clientParameter.MainValidateUrl = $"{baseUrl}/validate/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";
        //        //������ͻ��˸��°����ص�ַ
        //        //clientParameter.MainUpdateUrl = $"{baseUrl}/versions/{ (int)AppType.ClientApp }/{ mainVersion }/{clientParameter.AppSecretKey}";

        //        #endregion main app.

        //        var generalClientBootstrap = new GeneralClientBootstrap();
        //        //�����������°�����֪ͨ�¼�
        //        generalClientBootstrap.MultiDownloadProgressChanged += OnMultiDownloadProgressChanged;
        //        //�����������°������ٶȡ�ʣ�������¼�����ǰ���ذ汾��Ϣ֪ͨ�¼�
        //        generalClientBootstrap.MultiDownloadStatistics += OnMultiDownloadStatistics;
        //        //�����������°��������
        //        generalClientBootstrap.MultiDownloadCompleted += OnMultiDownloadCompleted;
        //        //������е���������֪ͨ
        //        generalClientBootstrap.MultiAllDownloadCompleted += OnMultiAllDownloadCompleted;
        //        //���ع��̳��ֵ��쳣֪ͨ
        //        generalClientBootstrap.MultiDownloadError += OnMultiDownloadError;
        //        //�������¹��̳��ֵ��κ����ⶼ��ͨ������¼�֪ͨ
        //        generalClientBootstrap.Exception += OnException;
        //        //ClientStrategy�ø��²��Խ����1.�Զ���������Ը��� 2.����������� 3.���ú�ClientParameter��������֮ǰ�İ汾дargs�������ͨѶ�ˡ�
        //        generalClientBootstrap.Config("", "").
        //        //generalClientBootstrap.Config(baseUrl,"appsecretkey").
        //        Option(UpdateOption.DownloadTimeOut, 60).
        //        Option(UpdateOption.Encoding, Encoding.Default).
        //        Option(UpdateOption.Format, "zip").
        //        //ע��һ��func���û������Ƿ��������θ��£������ǿ�Ƹ�������Ч
        //        SetCustomOption(ShowCustomOption).
        //        Strategy<ClientStrategy>();
        //        await generalClientBootstrap.LaunchTaskAsync();
        //    });
        //}

        ///// <summary>
        ///// ���û������Ƿ��������θ���
        ///// </summary>
        ///// <returns></returns>
        //private bool ShowCustomOption()
        //{
        //    var messageBoxResult = MessageBox.Show("��⵽������������汾��һ�£��Ƿ���£�", "click", MessageBoxButton.YesNoCancel);
        //    return messageBoxResult == MessageBoxResult.Yes;
        //}

        //private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        //{
        //    //e.Remaining ʣ������ʱ��
        //    //e.Speed �����ٶ�
        //    //e.Version ��ǰ���صİ汾��Ϣ
        //}

        //private void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        //{
        //    //e.TotalBytesToReceive ��ǰ���°���Ҫ���ص��ܴ�С
        //    //e.ProgressValue ��ǰ����ֵ
        //    //e.ProgressPercentage ��ǰ���ȵİٷֱ�
        //    //e.Version ��ǰ���صİ汾��Ϣ
        //    //e.Type ��ǰ����ִ�еĲ���  1.ProgressType.Check ���汾��Ϣ�� 2.ProgressType.Donwload �������ص�ǰ�汾 3. ProgressType.Updatefile ���µ�ǰ�汾 4. ProgressType.Done������� 5.ProgressType.Fail ����ʧ��
        //    //e.BytesReceived �����ش�С
        //}

        //private void OnException(object sender, ExceptionEventArgs e)
        //{
        //    Debug.WriteLine(e.Exception.Message);
        //}

        //private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
        //{
        //    //e.FailedVersions; �����������ʧ���������ش���İ汾������ԭ��ͳ�Ƶ��ü��ϵ��С�
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