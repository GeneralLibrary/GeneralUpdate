using GeneralUpdate.Core.OSS;
using GeneralUpdate.OSS;
using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.OSSStrategys;
using Xunit;

namespace TestOSS
{
    public class UnitTest1
    {
        [Fact]
        public  async void Test1()
        {
            try
            {
                string url = "http://192.168.50.203/";
                string appName = "MainApplication.exe";
                string currentVersion = "1.1.1.1";
                string versionFileName = "version_config.json";
                GeneralUpdateOSS.Download += OnOSSDownload;
                GeneralUpdateOSS.UnZipCompleted += OnOSSUnZipCompleted;
                GeneralUpdateOSS.UnZipProgress += OnOSSUnZipProgress;
                await GeneralUpdateOSS.Start<OSSStrategy>(new ParamsOSS(url, appName, currentVersion, versionFileName));
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
                //Assert.Fail(ex.Message);
            }
            await Console.Out.WriteLineAsync("done");
            //Assert.True(true);
        }

        private void OnOSSUnZipProgress(object sender, GeneralUpdate.Zip.Events.BaseUnZipProgressEventArgs e)
        {
            
        }

        private void OnOSSUnZipCompleted(object sender, GeneralUpdate.Zip.Events.BaseCompleteEventArgs e)
        {
            
        }

        private void OnOSSDownload(object sender, GeneralUpdate.OSS.Events.OSSDownloadArgs e)
        {
            Console.WriteLine($"{e.CurrentByte},{ e.TotalByte }");
        }
    }
}