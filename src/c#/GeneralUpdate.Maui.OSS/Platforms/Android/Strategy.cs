using Android.Content;
using Android.OS;
using GeneralUpdate.Core.Domain.DO;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.OSSArgs;
using GeneralUpdate.Maui.OSS.Domain.Entity;
using GeneralUpdate.Maui.OSS.Strategys;
using Java.IO;
using Java.Math;
using Java.Security;
using Newtonsoft.Json;
using System.Text;

namespace GeneralUpdate.Maui.OSS
{
    /// <summary>
    /// All the code in this file is only included on Android.
    /// </summary>
    public class Strategy : AbstractStrategy
    {
        #region Private Members

        private readonly string _appPath = FileSystem.AppDataDirectory;
        private const string _fromat = ".apk";
        private ParamsAndroid _parameter;

        #endregion

        #region Public Methods

        public override void Create<T>(T parameter)
        {
            _parameter = parameter as ParamsAndroid;
        }

        public override async Task Execute()
        {
            //1.Download the JSON version configuration file.
            var jsonUrl = $"{_parameter.Url}/{_parameter.VersionFileName}";
            var jsonPath = Path.Combine(_appPath, _parameter.VersionFileName);
            await DownloadFileAsync(jsonUrl, jsonPath, (readLength, totalLength)
                => EventManager.Instance.Dispatch<Action<object, OSSDownloadArgs>>(this, new OSSDownloadArgs(readLength, totalLength)));
            var jsonFile = new Java.IO.File(jsonPath);
            if (!jsonFile.Exists()) throw new Java.IO.FileNotFoundException(jsonPath);

            //2.Parse the JSON version configuration file content.
            byte[] jsonBytes = ReadFile(jsonFile);
            string json = Encoding.Default.GetString(jsonBytes);
            var versionConfig = JsonConvert.DeserializeObject<VersionConfigDO>(json);
            if (versionConfig == null) throw new NullReferenceException(nameof(versionConfig));

            //3.Compare with the latest version.
            var currentVersion = new Version(_parameter.CurrentVersion);
            var lastVersion = new Version(versionConfig.Version);
            if (currentVersion.Equals(lastVersion)) return;

            //4.Download the apk file.
            var file = Path.Combine(_appPath, $"{_parameter.Apk}{_fromat}");
            await DownloadFileAsync(versionConfig.Url, file, (readLength, totalLength)
                => EventManager.Instance.Dispatch<Action<object, OSSDownloadArgs>>(this, new OSSDownloadArgs(readLength, totalLength)));
            var apkFile = new Java.IO.File(file);
            if (!apkFile.Exists()) throw new Java.IO.FileNotFoundException(jsonPath);
            if (!versionConfig.MD5.Equals(GetFileMD5(apkFile, 64))) throw new Exception("The apk MD5 value does not match !");

            //5.Launch the apk to install.
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                intent.SetFlags(ActivityFlags.GrantReadUriPermission);//Give temporary read permissions.
                var uri = FileProvider.GetUriForFile(Android.App.Application.Context, _parameter.Authority, apkFile);
                intent.SetDataAndType(uri, "application/vnd.android.package-archive");//Sets the explicit MIME data type.
            }
            else
            {
                intent.SetDataAndType(Android.Net.Uri.FromFile(new Java.IO.File(file)), "application/vnd.android.package-archive");
            }
            intent.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Android OS read file bytes.
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
                throw new FileLoadException(ex.Message, ex.InnerException);
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
                throw new DigestException(ex.Message, ex.Cause);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e.InnerException);
            }
            var bigInt = new BigInteger(1, digest.Digest());
            return bigInt.ToString(radix);
        }

        #endregion
    }
}