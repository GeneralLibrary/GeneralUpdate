using GeneralUpdate.Extension;
using GeneralUpdate.Extension.DTOs;
using GeneralUpdate.Extension.Metadata;

namespace GeneralUpdate.Ext;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ExtensionHostConfig
        {
            HostVersion = new Version(1, 0, 0),
            InstallBasePath = @"C:\MyApp\Extensions",
            DownloadPath = @"C:\MyApp\Downloads",
            ServerUrl = "http://127.0.0.1/Extension/",  // New required field
            TargetPlatform = TargetPlatform.Windows,
            DownloadTimeout = 300,  // Applied to HTTP requests
            //AuthScheme = "Bearer",   // Optional authentication
            //AuthToken = "your-token" // Optional authentication
        };
        var host = new GeneralExtensionHost(config);
        var extensions = await host.QueryRemoteExtensions(new ExtensionQueryDTO(){  });
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