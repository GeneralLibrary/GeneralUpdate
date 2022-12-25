using Android.Content;
using Android.OS;
using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.OSS.Strategys;
using Newtonsoft.Json;
using System.Text;

namespace GeneralUpdate.OSS
{
    // All the code in this file is only included on Android.
    public class Strategy : AbstractStrategy
    {
        private readonly string _appPath = FileSystem.AppDataDirectory;
        private const string _fromat = ".apk";
        private string _url,_apk, _authority,_versionFileName,_currentVersion;

        public override void Create(params string[] arguments)
        {
            _url = arguments[0];
            _apk = arguments[1];
            _currentVersion = arguments[2];
            _authority = arguments[3];
            _versionFileName = arguments[4];
        }

        public override async Task Excute()
        {
            //1.Download the JSON version configuration file.
            var jsonUrl = $"{_url}/{_versionFileName}";
            var jsonPath = Path.Combine(_appPath, _versionFileName);
            await DownloadFileAsync(jsonUrl, jsonPath, null);
            var jsonFile = new Java.IO.File(jsonPath);
            if (!jsonFile.Exists()) throw new Java.IO.FileNotFoundException(jsonPath);

            //2.Parse the JSON version configuration file content.
            byte[] jsonBytes = ReadFile(jsonFile);
            string json = Encoding.Default.GetString(jsonBytes);
            var versionConfig = JsonConvert.DeserializeObject<VersionConfigDO>(json);
            if(versionConfig == null) throw new NullReferenceException(nameof(versionConfig));

            //3.Compare with the latest version.
            var currentVersion = new Version(_currentVersion);
            var lastVersion = new Version(versionConfig.Version);
            if (currentVersion.Equals(lastVersion)) return;

            //4.Download the apk file.
            var file = $"{_appPath}/{_apk}{_fromat}";
            await DownloadFileAsync(versionConfig.Url, file, null);
            var apkFile = new Java.IO.File(file);
            if(!apkFile.Exists()) throw new Java.IO.FileNotFoundException(jsonPath);

            //5.Launch the apk to install.
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                intent.SetFlags(ActivityFlags.GrantReadUriPermission);//Give temporary read permissions.
                var uri = FileProvider.GetUriForFile(Android.App.Application.Context, _authority, apkFile);
                intent.SetDataAndType(uri, "application/vnd.android.package-archive");//Sets the explicit MIME data type.
            }
            else 
            {
                intent.SetDataAndType(Android.Net.Uri.FromFile(new Java.IO.File(file)), "application/vnd.android.package-archive");
            }
            intent.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }

        private byte[] ReadFile(Java.IO.File file) 
        {
            var buffer = new byte[1024];
            try
            {
                Java.IO.FileInputStream inputStream = new Java.IO.FileInputStream(file);
                if (file.IsDirectory) return null;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) 
                {
                    return inputStream.ReadAllBytes();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}