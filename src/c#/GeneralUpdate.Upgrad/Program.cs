using GeneralUpdate.Core;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System.Text;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(args[0]);
            Thread.Sleep(5000);
            Task.Run(async () =>
            {
                //var arg = "eyJBcHBUeXBlIjoxLCJBcHBOYW1lIjoiR2VuZXJhbFVwZGF0ZS5DbGllbnQiLCJJbnN0YWxsUGF0aCI6IkY6XFxnaXRfcHJvamVjdFxcR2VuZXJhbFVwZGF0ZVxcc3JjXFxjI1xcR2VuZXJhbFVwZGF0ZS5DbGllbnRcXGJpblxcRGVidWdcXG5ldDYuMC13aW5kb3dzMTAuMC4xOTA0MS4wXFx3aW4xMC14NjRcXEFwcFhcXCIsIkN1cnJlbnRWZXJzaW9uIjoiMS4wLjAuMCIsIkxhc3RWZXJzaW9uIjoiOS45LjkuOSIsIkxvZ1VybCI6bnVsbCwiSXNVcGRhdGUiOmZhbHNlLCJDb21wcmVzc0VuY29kaW5nIjo3LCJDb21wcmVzc0Zvcm1hdCI6bnVsbCwiRG93bmxvYWRUaW1lT3V0IjowLCJBcHBTZWNyZXRLZXkiOiJCOEE3RkFERC0zODZDLTQ2QjAtQjI4My1DOUY5NjM0MjBDN0MiLCJVcGRhdGVWZXJzaW9ucyI6W3siUHViVGltZSI6MTY2NDA5NjUyMCwiTmFtZSI6IjE2NjQwODEzMTUiLCJNRDUiOiJkZDc3NmUzYTRmMjAyOGE1ZjYxMTg3ZTIzMDg5ZGRiZCIsIlZlcnNpb24iOiIwLjAuMC4wIiwiVXJsIjoiaHR0cDovLzEyNy4wLjAuMS8xNjY0MDgzMTI2LnppcCIsIklEIjpudWxsfV0sIklEIjpudWxsfQ==";
                var bootStrap = new GeneralUpdateBootstrap();
                bootStrap.MutiAllDownloadCompleted += OnMutiAllDownloadCompleted;
                bootStrap.MutiDownloadCompleted += OnMutiDownloadCompleted;
                bootStrap.MutiDownloadError += OnMutiDownloadError;
                bootStrap.MutiDownloadProgressChanged += OnMutiDownloadProgressChanged;
                bootStrap.MutiDownloadStatistics += OnMutiDownloadStatistics;
                bootStrap.Exception += OnException;
                bootStrap.Strategy<WindowsStrategy>().
                Option(UpdateOption.Encoding, Encoding.Default).
                Option(UpdateOption.DownloadTimeOut, 60).
                Option(UpdateOption.Format, Format.ZIP).
                Remote(args[0]);
                await bootStrap.LaunchTaskAsync();
            });
            Console.Read();
        }

        private static void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        {
            Console.WriteLine($" {e.Speed} , {e.Remaining.ToShortTimeString()}");
        }

        private static void OnMutiDownloadProgressChanged(object sender, GeneralUpdate.Core.Bootstrap.MutiDownloadProgressChangedEventArgs e)
        {
            switch (e.Type)
            {
                case ProgressType.Check:
                    break;

                case ProgressType.Donwload:
                    Console.WriteLine($" {Math.Round(e.ProgressValue * 100, 2)}% ， Receivedbyte：{e.BytesReceived}M ，Totalbyte：{e.TotalBytesToReceive}M");
                    break;

                case ProgressType.Updatefile:
                    break;

                case ProgressType.Done:
                    break;

                case ProgressType.Fail:
                    break;
            }
        }

        private static void OnMutiDownloadCompleted(object sender, GeneralUpdate.Core.Bootstrap.MutiDownloadCompletedEventArgs e)
        {
            var info = e.Version as VersionInfo;
            Console.WriteLine($"{info.Name} download completed.");
        }

        private static void OnMutiAllDownloadCompleted(object sender, GeneralUpdate.Core.Bootstrap.MutiAllDownloadCompletedEventArgs e)
        {
            Console.WriteLine($"AllDownloadCompleted {e.IsAllDownloadCompleted}");
        }

        private static void OnMutiDownloadError(object sender, GeneralUpdate.Core.Bootstrap.MutiDownloadErrorEventArgs e)
        {
            var info = e.Version as VersionInfo;
            Console.WriteLine($"{info.Name},{e.Exception.Message}.");
        }

        private static void OnException(object sender, GeneralUpdate.Core.Bootstrap.ExceptionEventArgs e)
        {
            Console.WriteLine($"{e.Exception.Message}");
        }
    }
}