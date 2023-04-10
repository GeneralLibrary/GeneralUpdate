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
                await GeneralUpdateOSS.Start<Strategy>(new ParamsAndroid("", "", "", "", ""));
            });
        }
    }
}