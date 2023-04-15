using GeneralUpdate.Maui.OSS;
using GeneralUpdate.Maui.OSS.Domain.Entity;

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
                var url = "http://192.168.50.203";
                var apk = "GeneralUpdate.Client.apk";
                var authority = "com.generalupdate.oss.fileprovider";
                var currentVersion = "1.0.0.0";
                var versionFileName = "version.json";
                await GeneralUpdateOSS.Start<Strategy>(new ParamsAndroid(url, apk, authority, currentVersion, versionFileName));
            });
        }
    }
}