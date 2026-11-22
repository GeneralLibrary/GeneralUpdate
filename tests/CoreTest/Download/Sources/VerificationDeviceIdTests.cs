using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.Security;

namespace CoreTest.Download.Sources;

public sealed class VerificationDeviceIdTests
{
    [Fact]
    public async Task BothVerificationRequestsCarrySameDeviceId()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var id = Guid.NewGuid().ToString("D");
        var receiver = ReceiveRequests(listener);
        var source = new HttpDownloadSource($"http://127.0.0.1:{port}/verification", "1.0.0",
            "1.0.0", "test-key", PlatformType.Windows, "test", null, null, AuthScheme.Hmac, null, null)
        {
            DeviceId = id
        };
        await source.ListAsync().WaitAsync(TimeSpan.FromSeconds(20));
        var requests = await receiver.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal(id, (string?)request["deviceId"]));
        Assert.NotEqual((int?)requests[0]["appType"], (int?)requests[1]["appType"]);
    }

    private static async Task<List<JsonNode>> ReceiveRequests(TcpListener listener)
    {
        var requests = new List<JsonNode>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (var i = 0; i < 2; i++)
        {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var length = 0;
            while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 } header)
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    length = int.Parse(header["Content-Length:".Length..].Trim());
            var buffer = new char[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(offset), timeout.Token);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            requests.Add(JsonNode.Parse(new string(buffer))!);
            var body = "{\"code\":200,\"body\":[]}";
            var response = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(response, timeout.Token);
        }
        return requests;
    }
}
