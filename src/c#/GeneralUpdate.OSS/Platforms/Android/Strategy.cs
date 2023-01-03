using Android.Content;
using Android.OS;
using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.OSS.OSSStrategys;
using Java.IO;
using Java.Math;
using Java.Security;
using Newtonsoft.Json;
using System.Text;

namespace GeneralUpdate.OSS
{
    // All the code in this file is only included on Android.
    public class Strategy : AbstractStrategy
    {
        private readonly string _appPath = FileSystem.AppDataDirectory;
        private const string _fromat = ".apk";
        private string _url, _apk, _authority, _versionFileName, _currentVersion;

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
            if (versionConfig == null) throw new NullReferenceException(nameof(versionConfig));

            //3.Compare with the latest version.
            var currentVersion = new Version(_currentVersion);
            var lastVersion = new Version(versionConfig.Version);
            if (currentVersion.Equals(lastVersion)) return;

            //4.Download the apk file.
            var file = $"{_appPath}/{_apk}{_fromat}";
            await DownloadFileAsync(versionConfig.Url, file, null);
            var apkFile = new Java.IO.File(file);
            if (!apkFile.Exists()) throw new Java.IO.FileNotFoundException(jsonPath);
            if (!versionConfig.MD5.Equals(GetFileMD5(apkFile, 64))) throw new Exception("The apk MD5 value does not match !");

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

        /// <summary>
        /// Android OS read file byts.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private byte[] ReadFile(Java.IO.File file)
        {
            try
            {
                var fileLength = file.Length();
                var buffer = new byte[fileLength];
                var inputStream = new Java.IO.FileInputStream(file);
                if (file.IsDirectory) return null;
                inputStream.Read(buffer, 0, (int)fileLength);
                inputStream.Close();
                return buffer;
            }
            catch (FileLoadException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }
        }

        /// <summary>
        /// Example Get the md5 value of the file.
        /// </summary>
        /// <param name="file">target file.</param>
        /// <param name="radix">radix 16 32 64</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetFileMD5(Java.IO.File file, int radix)
        {
            if (!file.IsFile) return null;
            MessageDigest digest = null;
            FileInputStream inputStream = null;
            byte[] buffer = new byte[1024];
            int len;
            try
            {
                digest = MessageDigest.GetInstance("MD5");
                inputStream = new FileInputStream(file);
                while ((len = inputStream.Read(buffer, 0, 1024)) != -1)
                {
                    digest.Update(buffer, 0, len);
                }
                inputStream.Close();
            }
            catch (DigestException ex)
            {
                throw ex;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e.InnerException);
            }
            BigInteger bigInt = new BigInteger(1, digest.Digest());
            return bigInt.ToString(radix);
        }
    }
}