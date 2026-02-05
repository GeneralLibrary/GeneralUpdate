using GeneralUpdate.Extension;
using GeneralUpdate.Extension.Metadata;

namespace GeneralUpdate.Ext;

class Program
{
    static async Task Main(string[] args)
    {
        var installPath = @"C:\data\install";
        var downloadPath = @"C:\data\download";
        var host = new GeneralExtensionHost(new Version("1.0.0"), installPath, downloadPath);
        host.InstallationCompleted += (_, _) =>
        {
            Console.WriteLine("Installation completed");
        };
        host.DownloadCompleted += (_, _) =>
        {
            Console.WriteLine("Download completed");
        };
        host.DownloadProgress += (_, _) =>
        {
            Console.WriteLine("Downloading...");
        };

        host.QueueUpdate(new AvailableExtension());
        await host.ProcessNextUpdateAsync();

        Console.Read();
    }
}