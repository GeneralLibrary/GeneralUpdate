using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.OSSArgs;
using GeneralUpdate.Maui.OSS;
using GeneralUpdate.Maui.OSS.Domain.Entity;
using System;

namespace TestMauiApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            //http://192.168.50.203/version.json
            string url = "http://192.168.50.203";
            string appName = "MainApplication.exe";
            string currentVersion = "1.1.1.1";
            string versionFileName = "versions.json";
            GeneralUpdateOSS.AddListenerDownloadProcess(OnOSSDownload);
            GeneralUpdateOSS.AddListenerException(OnException);
            await GeneralUpdateOSS.Start<Strategy>(new ParamsAndroid(url, appName, "123456789", currentVersion, versionFileName));
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