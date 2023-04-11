using GeneralUpdate.Maui.OSS;
using GeneralUpdate.Maui.OSS.Domain.Entity;

namespace GeneralUpdate.OSSClient
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

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
                var authority = "B8A7FADD-386C-46B0-B283-C9F963420C7C";
                var currentVersion = "v1.0.0.0";
                var versionFileName = "GeneralUpdate.Client.apk";
                await GeneralUpdateOSS.Start<Strategy>(new ParamsAndroid(url, apk, authority, currentVersion, versionFileName));
            });
        }
    }
}