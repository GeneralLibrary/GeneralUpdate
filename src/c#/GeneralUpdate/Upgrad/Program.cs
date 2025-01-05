using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Upgrad.Strategys;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine($"升级程序初始化，{DateTime.Now}！");
                Console.WriteLine("当前运行目录：" + Thread.GetDomain().BaseDirectory);
                //Console.WriteLine("请按任意键继续……");
                //Console.ReadKey();

                var json = GetProcessInfoJsonContext();
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("json environment variable is not defined");

                var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);
                if (processInfo == null)
                    throw new ArgumentException("ProcessInfo object cannot be null!");
                var _configInfo = new GlobalConfigInfo()
                {
                    MainAppName = processInfo.AppName,
                    InstallPath = processInfo.InstallPath,
                    ClientVersion = processInfo.CurrentVersion,
                    LastVersion = processInfo.LastVersion,
                    UpdateLogUrl = processInfo.UpdateLogUrl,
                    Encoding = Encoding.GetEncoding(processInfo.CompressEncoding),
                    Format = processInfo.CompressFormat,
                    DownloadTimeOut = processInfo.DownloadTimeOut,
                    AppSecretKey = processInfo.AppSecretKey,
                    UpdateVersions = processInfo.UpdateVersions,
                    PatchPath = processInfo.PatchPath,
                    ReportUrl = processInfo.ReportUrl,
                    BackupDirectory = processInfo.BackupDirectory,
                    Scheme = processInfo.Scheme,
                    Token = processInfo.Token,
                    TempPath = processInfo.TempPath,
                };
                IStrategy? _strategy = StrategyFactory();
                if(_strategy == null)
                    throw new ArgumentNullException("Strategy object cannot be null!");

                //这里完善下传参
                _strategy.Create(_configInfo);
                await _strategy?.Execute();
                //_strategy?.StartApp();
                //Console.ReadKey();
                Console.WriteLine($"升级程序已启动，{DateTime.Now}！");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
            }
            

        }
        public static IStrategy? StrategyFactory()
        {
            try
            {
                IStrategy? _strategy;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    _strategy = new WindowsStrategy();
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    _strategy = new LinuxStrategy();
                else
                    throw new PlatformNotSupportedException("The current operating system is not supported!");

                return _strategy;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                //EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception, exception.Message));
            }
            return null;
        }

        private static void OnException(object arg1, ExceptionEventArgs arg2)
        {
            Console.WriteLine($"{arg2.Exception}");
        }
        private static string? GetProcessInfoJsonContext()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.GetEnvironmentVariable("ProcessInfo", EnvironmentVariableTarget.User);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var jsonFileName = "ProcessInfo.json";
                if (File.Exists(jsonFileName))
                    return File.ReadAllText(jsonFileName);
            }

            return null;
        }
    }
}