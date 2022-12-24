using Android.Content;
using Android.OS;
using GeneralUpdate.OSS.Strategys;

namespace GeneralUpdate.OSS
{
    // All the code in this file is only included on Android.
    public class Strategy : IStrategy
    {
        private readonly string appPath = FileSystem.AppDataDirectory;
        private string apk = ".apk";

        public void Create()
        {
            throw new NotImplementedException();
        }

        public void Excute()
        {
            var file = $"{appPath}/{apk}";
            var apkFile = new Java.IO.File(file);
            var intent = new Intent(Intent.ActionView);
            //Give temporary read permissions.
            intent.SetFlags(ActivityFlags.GrantReadUriPermission);
            var uri = FileProvider.GetUriForFile(Android.App.Application.Context, "com.masa.mauidemo.fileprovider", apkFile);
            //Sets the explicit MIME data type.
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            //intent.SetDataAndType(Android.Net.Uri.FromFile(new Java.IO.File(file)), "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }

        public string GetVersion() => VersionTracking.CurrentVersion;
    }
}