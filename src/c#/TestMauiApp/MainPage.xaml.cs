using GeneralUpdate.Maui.OSS;
using GeneralUpdate.Maui.OSS.Domain.Entity;
using GeneralUpdate.Maui.OSS.Events;

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
            GeneralUpdateOSS.Download += OnOSSDownload;
            GeneralUpdateOSS.UnZipCompleted += OnOSSUnZipCompleted;
            GeneralUpdateOSS.UnZipProgress += OnOSSUnZipProgress;
            await GeneralUpdateOSS.Start<Strategy>(new ParamsWindows(url, appName, currentVersion, versionFileName));
        }

        private void OnOSSUnZipProgress(object sender, GeneralUpdate.Zip.Events.BaseUnZipProgressEventArgs e)
        {
        }

        private void OnOSSUnZipCompleted(object sender, GeneralUpdate.Zip.Events.BaseCompleteEventArgs e)
        {
        }

        private void OnOSSDownload(object sender, OSSDownloadArgs e)
        {
            Console.WriteLine($"{e.CurrentByte},{e.TotalByte}");
        }
    }
}