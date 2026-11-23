using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;

namespace ReportWorker;

public sealed class Marker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--dll-search-probe")
        {
            if (!OperatingSystem.IsWindows()) return 3;
            var module = LoadLibrary(args[1]);
            if (module == IntPtr.Zero)
            {
                Console.WriteLine($"error:{Marshal.GetLastWin32Error()}");
                return 0;
            }
            FreeLibrary(module);
            Console.WriteLine("loaded");
            return 0;
        }
        if (args.Length == 4 && args[0] == "--handle-list-probe")
        {
            if (!OperatingSystem.IsWindows()) return 3;
            File.WriteAllLines(args[3], new[] { HandlePath(args[1]), HandlePath(args[2]) });
            return 0;
        }
        if (args.Length == 1 && args[0] == "--image-lock-peer")
        {
            Console.WriteLine("ready");
            await Console.Out.FlushAsync();
            await Console.In.ReadLineAsync();
            return 0;
        }
        if (args.Length != 2) return 2;
        using var client = new HttpClient(new Handler(args[1]));
        var reporter = new OrderedUpdateReporter(args[0], client, "https://example.test/report", "tenant/database", true);
        try
        {
            await reporter.ReportAsync(new UpdateReport(10, 3));
        }
        catch (HttpRequestException)
        {
            return 0;
        }
        return 0;
    }

    private static string HandlePath(string value)
    {
        var path = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(new IntPtr(long.Parse(value)), path, (uint)path.Capacity, 0);
        return length == 0 ? "" : path.ToString();
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(IntPtr handle, StringBuilder path, uint size, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    private sealed class Handler(string mode) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            if (json.RootElement.GetProperty("eventId").GetGuid() == Guid.Empty ||
                json.RootElement.GetProperty("sequence").GetInt64() <= 0)
                throw new InvalidDataException("Invalid ordered report.");
            if (mode == "hold")
            {
                Console.WriteLine("committed");
                await Console.Out.FlushAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            if (mode == "offline") throw new HttpRequestException("offline");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"code\":200,\"body\":true}") };
        }
    }
}
