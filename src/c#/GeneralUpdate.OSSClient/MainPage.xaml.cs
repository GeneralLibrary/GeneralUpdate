using GeneralUpdate.Common.Internal;
using GeneralUpdate.Maui.OSS;
using GeneralUpdate.Maui.OSS.Internal;

namespace GeneralUpdate.OSSClient
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                var paramsAndroid = new ParamsAndroid
                {
                    Url = "http://192.168.50.203",
                    Apk = "com.companyname.generalupdate.ossclient.apk",
                    Authority = "com.generalupdate.oss.fileprovider",
                    CurrentVersion = "1.0.0.0",
                    VersionFileName = "version.json"
                };
                GeneralUpdateOSS.AddListenerDownloadProcess(OnOSSDownload);
                GeneralUpdateOSS.AddListenerException(OnException);
                await GeneralUpdateOSS.Start<Strategy>(paramsAndroid);
            });
        }

        private void OnOSSDownload(object sender, OSSDownloadArgs e)
        {
            Console.WriteLine($"{e.ReadLength},{e.TotalLength}");
        }

        private void OnException(object sender, ExceptionEventArgs exception)
        {
            Console.WriteLine(exception.Exception.Message);
        }
    }
}