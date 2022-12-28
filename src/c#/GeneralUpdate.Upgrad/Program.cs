using GeneralUpdate.Core;
using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Strategys.PlatformWindows;
using System.Text;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(args[0]);
            Thread.Sleep(5000);
            Task.Run(async () =>
            {
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
            //var info = e.Version as GeneralUpdate.Core.Domain.Entity.VersionInfo;
            //Console.WriteLine($"{info.Name} download completed.");
        }

        private static void OnMutiAllDownloadCompleted(object sender, GeneralUpdate.Core.Bootstrap.MutiAllDownloadCompletedEventArgs e)
        {
            Console.WriteLine($"AllDownloadCompleted {e.IsAllDownloadCompleted}");
        }

        private static void OnMutiDownloadError(object sender, GeneralUpdate.Core.Bootstrap.MutiDownloadErrorEventArgs e)
        {
            //var info = e.Version as GeneralUpdate.Core.Domain.Entity.VersionInfo;
            //Console.WriteLine($"{info.Name},{e.Exception.Message}.");
        }

        private static void OnException(object sender, GeneralUpdate.Core.Bootstrap.ExceptionEventArgs e)
        {
            Console.WriteLine($"{e.Exception.Message}");
        }
    }
}