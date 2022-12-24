using Android.Content;
using Android.OS;
using GeneralUpdate.OSS.Strategys;
using Java.Net;

namespace GeneralUpdate.OSS
{
    // All the code in this file is only included on Android.
    public class Strategy : AbstractStrategy
    {
        private readonly string _appPath = FileSystem.AppDataDirectory;
        private const string _fromat = ".apk";
        private string _url,_apk, _authority,_versionFileName;

        public override void Create(params string[] arguments)
        {
            _url = arguments[0];
            _apk = arguments[1];
            _authority = arguments[2];
            _versionFileName = arguments[3];
        }

        public override void Excute()
        {
            var file = $"{_appPath}/{_apk}{_fromat}";
            var apkFile = new Java.IO.File(file);
            var intent = new Intent(Intent.ActionView);
            //Give temporary read permissions.
            intent.SetFlags(ActivityFlags.GrantReadUriPermission);
            var uri = FileProvider.GetUriForFile(Android.App.Application.Context, _authority, apkFile);
            //Sets the explicit MIME data type.
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }

        public string GetVersion() => VersionTracking.CurrentVersion;
    }
}