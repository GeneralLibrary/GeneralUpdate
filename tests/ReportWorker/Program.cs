using System.Net;
using System.Text.Json;
using GeneralUpdate.Core.Download.Reporting;

namespace ReportWorker;

public sealed class Marker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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
