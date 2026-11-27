using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.JsonContext;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreTest.Hubs;

[CollectionDefinition("Push process", DisableParallelization = true)]
public class PushProcessCollection;

[Collection("Push process")]
public class PushProcessIntegrationTests
{
    [Fact]
    public async Task ClientPush_AutomaticallyLaunchesUpdater_WithTwoChecksWithoutCredentials()
    {
        await using var test = await PushFixture.StartAsync();
        await test.SendAsync(test.Packet(AppType.Client, 123));
        await test.WaitForFileAsync("updater-finished");

        Assert.Equal("updated", await File.ReadAllTextAsync(Path.Combine(test.Root, "marker.txt")));
        Assert.NotEqual(await File.ReadAllTextAsync(Path.Combine(test.Root, "client-ready")),
            await File.ReadAllTextAsync(Path.Combine(test.Root, "updater-started")));
        Assert.Equal(2, test.State.Validations.Count);
        Assert.All(test.State.Validations, request =>
        {
            Assert.Equal(123, request.RecordId);
            Assert.Equal(PushFixture.AppKey, request.AppKey);
            Assert.Equal(test.Hash, request.PacketHash);
            Assert.Equal("", request.Tenant);
            Assert.False(request.MarkerExists);
        });
        Assert.Contains(test.State.Reports, report =>
            report.RecordId == 123 && report.Type == 2 && report.Status == 2 && report.MarkerExists);
        Assert.All(test.State.Reports, report => Assert.Equal("", report.Tenant));
        Assert.Equal(0, test.State.Verifications);
    }

    [Theory]
    [InlineData("GU_PUSH_TENANT")]
    [InlineData("GU_PUSH_TOKEN")]
    [InlineData("GU_PUSH_BASIC_USERNAME")]
    [InlineData("GU_PUSH_BASIC_PASSWORD")]
    public async Task CredentialBearingConfiguration_FailsClosedBeforeListening(string credentialSetting)
    {
        await using var test = await PushFixture.StartAsync(credentialSetting: credentialSetting);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await test.Client.WaitForExitAsync(timeout.Token);
        Assert.NotEqual(0, test.Client.ExitCode);
        Assert.False(File.Exists(Path.Combine(test.Root, "client-ready")));
        Assert.Empty(test.State.Validations);
        Assert.Equal(0, test.State.Downloads);
        Assert.Empty(test.State.Reports);
    }

    [Fact]
    public async Task RevokedAfterDownload_RejectsInActualUpdater_BeforeFilesOrTerminalReports()
    {
        await using var test = await PushFixture.StartAsync(pause: true);
        await test.SendAsync(test.Packet(AppType.Client, 124));
        await test.WaitForFileAsync("before-apply");
        Assert.Single(test.State.Validations);
        test.State.ValidationStatus = 403;
        File.WriteAllText(Path.Combine(test.Root, "resume-apply"), "");
        await test.WaitForFileAsync("updater-finished");

        Assert.Equal(2, test.State.Validations.Count);
        Assert.False(File.Exists(Path.Combine(test.Root, "marker.txt")));
        Assert.DoesNotContain(test.State.Reports, report => report.Status is 2 or 3);
    }

    [Fact]
    public async Task ValidationTimeout_RejectsWithoutDownload_AndQueueContinues()
    {
        await using var test = await PushFixture.StartAsync();
        test.State.ValidationDelay = TimeSpan.FromSeconds(15);
        await test.SendAsync(test.Packet(AppType.Client, 128));
        await test.WaitUntilAsync(() => test.State.Validations.Count == 1);
        test.State.ValidationDelay = TimeSpan.Zero;
        await test.SendAsync(test.Packet(AppType.Upgrade, 129));
        await test.WaitUntilAsync(() => test.State.Reports.Any(r => r.RecordId == 129 && r.Status == 2));

        Assert.Equal(1, test.State.Downloads);
        Assert.DoesNotContain(test.State.Reports, report => report.RecordId == 128);
        Assert.False(File.Exists(Path.Combine(test.Root, "updater-started")));
    }

    [Fact]
    public async Task DownloadRedirect_DoesNotForwardTenantHeadersOrApplyFiles()
    {
        await using var test = await PushFixture.StartAsync();
        test.State.RedirectDownload = true;
        await test.SendAsync(test.Packet(AppType.Client, 130));
        await test.WaitUntilAsync(() => test.State.Reports.Any(r => r.RecordId == 130 && r.Status == 3));

        Assert.Equal(0, test.State.Redirects);
        Assert.False(File.Exists(Path.Combine(test.Root, "marker.txt")));
        Assert.False(File.Exists(Path.Combine(test.Root, "updater-started")));
    }

    [Fact]
    public async Task InvalidAndLegacyMessages_AreRejected_ThenUpgradePacketsAreSerialized()
    {
        await using var test = await PushFixture.StartAsync();
        var legacy = test.Packet(AppType.Client, 0);
        legacy.RecordId = null;
        await test.SendAsync(legacy);
        var wrongProduct = test.Packet(AppType.Client, 10);
        wrongProduct.ProductId = "different";
        await test.SendAsync(wrongProduct);
        var wrongPlatform = test.Packet(AppType.Client, 11);
        wrongPlatform.Platform = 0;
        await test.SendAsync(wrongPlatform);
        var chain = test.Packet(AppType.Client, 12);
        chain.PackageType = 1;
        await test.SendAsync(chain);
        var wrongOrigin = test.Packet(AppType.Client, 13);
        wrongOrigin.Url = "http://127.0.0.1:1/package.zip";
        await test.SendAsync(wrongOrigin);
        var wrongFormat = test.Packet(AppType.Client, 14);
        wrongFormat.Format = "tar";
        await test.SendAsync(wrongFormat);
        await test.SendAsync(test.Packet(AppType.Upgrade, 201));
        var next = test.Packet(AppType.Upgrade, 202);
        next.Version = "3.0.0";
        next.Name = "package-next.zip";
        await test.SendAsync(next);
        await test.WaitUntilAsync(() => test.State.Reports.Any(r => r.RecordId == 202 && r.Status == 2));

        Assert.Equal(new[] { 201, 201, 202, 202 }, test.State.Validations.Select(v => v.RecordId));
        Assert.Equal(2, test.State.Downloads);
        Assert.True(File.Exists(Path.Combine(test.Root, "updater", "marker.txt")));
        Assert.False(File.Exists(Path.Combine(test.Root, "updater-started")));
        Assert.Equal(0, test.State.Verifications);
    }

    [Theory]
    [InlineData(200, """{"code":400,"body":false}""")]
    [InlineData(200, """{"code":200,"body":false}""")]
    [InlineData(200, """{"body":true}""")]
    [InlineData(200, """{"code":200}""")]
    [InlineData(200, "null")]
    [InlineData(200, "{")]
    [InlineData(403, """{"code":200,"body":true}""")]
    [InlineData(404, """{"code":200,"body":true}""")]
    [InlineData(500, """{"code":200,"body":true}""")]
    [InlineData(307, """{"code":200,"body":true}""")]
    public async Task NonApproval_FailsClosed_BeforeDownload(int status, string body)
    {
        await using var test = await PushFixture.StartAsync();
        test.State.ValidationStatus = status;
        test.State.ValidationBody = body;
        await test.SendAsync(test.Packet(AppType.Client, 126));
        await test.WaitUntilAsync(() => test.State.Validations.Count == 1);
        // A later accepted packet is the queue barrier: it must execute after the rejected packet.
        test.State.ValidationStatus = 200;
        test.State.ValidationBody = """{"code":200,"body":true}""";
        await test.SendAsync(test.Packet(AppType.Upgrade, 127));
        await test.WaitUntilAsync(() => test.State.Reports.Any(r => r.RecordId == 127 && r.Status == 2));

        Assert.Equal(1, test.State.Downloads);
        Assert.DoesNotContain(test.State.Reports, report => report.RecordId == 126);
        Assert.False(File.Exists(Path.Combine(test.Root, "updater-started")));
        Assert.Equal(0, test.State.Redirects);
    }

    private sealed class PushFixture : IAsyncDisposable
    {
        public const string AppKey = "registered-push-client";
        public required string Root { get; init; }
        public required string Url { get; init; }
        public required string Hash { get; init; }
        public required WebApplication App { get; init; }
        public required Process Client { get; init; }
        public required ServerState State { get; init; }
        private readonly ConcurrentQueue<string> _output = new();

        public static async Task<PushFixture> StartAsync(bool pause = false, string? credentialSetting = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "GeneralUpdate.PushProcess", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "updater"));
            var host = Environment.GetEnvironmentVariable("GENERALUPDATE_PUSH_HOST_PATH") ??
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                    "..", "..", "..", "..", "PushAotSmoke", "bin", "Release", "net10.0",
                    OperatingSystem.IsWindows() ? "PushAotSmoke.exe" : "PushAotSmoke"));
            Assert.True(File.Exists(host), $"Build or publish the push host first: {host}");
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(host)!))
                File.Copy(file, Path.Combine(root, "updater", Path.GetFileName(file)), true);
            var updater = Path.Combine(root, "updater", OperatingSystem.IsWindows() ? "PushUpdater.exe" : "PushUpdater");
            File.Copy(host, updater, true);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(updater, File.GetUnixFileMode(host));

            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("marker.txt").Open());
                writer.Write("updated");
            }
            var bytes = archive.ToArray();
            var state = new ServerState { Root = root, Zip = bytes };
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSignalR();
            builder.Services.AddSingleton(state);
            var app = builder.Build();
            app.MapHub<PushTestHub>("/UpgradeHub");
            app.MapPost("/Upgrade/Verification", () =>
            {
                Interlocked.Increment(ref state.Verifications);
                return Microsoft.AspNetCore.Http.Results.Json(new { code = 404, body = Array.Empty<object>() });
            });
            app.MapPost("/Upgrade/ValidatePush", async (Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                var request = await JsonSerializer.DeserializeAsync(context.Request.Body,
                    PushPayloadJsonContext.Default.PushValidationRequest);
                var status = state.ValidationStatus;
                var body = state.ValidationBody;
                var delay = state.ValidationDelay;
                Assert.False(context.Request.Headers.ContainsKey("X-Poison"));
                Assert.False(context.Request.Headers.ContainsKey("Authorization"));
                Assert.False(context.Request.Headers.ContainsKey("X-Update-Signature"));
                state.Validations.Enqueue(new(request!.RecordId, request.AppKey, request.PacketHash,
                    context.Request.Headers["X-Tenant-Id"].ToString(),
                    File.Exists(Path.Combine(root, "marker.txt"))));
                if (delay > TimeSpan.Zero) await Task.Delay(delay, context.RequestAborted);
                context.Response.StatusCode = status;
                if (status == 307) context.Response.Headers.Location = "/redirected";
                context.Response.ContentType = "application/json";
                await Microsoft.AspNetCore.Http.HttpResponseWritingExtensions.WriteAsync(context.Response, body);
            });
            app.MapGet("/package.zip", (Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                Assert.Equal("", context.Request.Headers["X-Tenant-Id"].ToString());
                Assert.False(context.Request.Headers.ContainsKey("X-Poison"));
                Assert.False(context.Request.Headers.ContainsKey("Authorization"));
                Assert.False(context.Request.Headers.ContainsKey("X-Update-Signature"));
                Interlocked.Increment(ref state.Downloads);
                if (state.RedirectDownload)
                    return Microsoft.AspNetCore.Http.Results.Redirect("/redirected", permanent: false, preserveMethod: true);
                return Microsoft.AspNetCore.Http.Results.Bytes(bytes, "application/zip");
            });
            app.MapPost("/redirected", () => Interlocked.Increment(ref state.Redirects));
            app.MapGet("/redirected", () => Interlocked.Increment(ref state.Redirects));
            app.MapPost("/Upgrade/Report", async (Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body);
                Assert.False(context.Request.Headers.ContainsKey("X-Poison"));
                var value = document.RootElement;
                state.Reports.Enqueue(new(value.GetProperty("recordId").GetInt32(),
                    value.GetProperty("status").GetInt32(), value.GetProperty("type").GetInt32(),
                    context.Request.Headers["X-Tenant-Id"].ToString(),
                    File.Exists(Path.Combine(root, "marker.txt")) ||
                    File.Exists(Path.Combine(root, "updater", "marker.txt"))));
                return Microsoft.AspNetCore.Http.Results.Ok();
            });
            await app.StartAsync();
            var url = app.Urls.Single();
            var start = new ProcessStartInfo(host, "client")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.Environment["GU_PUSH_ROOT"] = root;
            start.Environment["GU_PUSH_SERVER"] = url;
            start.Environment["GU_PUSH_APPKEY"] = AppKey;
            start.Environment["GU_PUSH_PAUSE_APPLY"] = pause ? "1" : "0";
            start.Environment.Remove("GU_PUSH_TENANT");
            start.Environment.Remove("GU_PUSH_TOKEN");
            start.Environment.Remove("GU_PUSH_BASIC_USERNAME");
            start.Environment.Remove("GU_PUSH_BASIC_PASSWORD");
            if (credentialSetting != null)
                start.Environment[credentialSetting] = "sensitive-test-value";
            var process = Process.Start(start)!;
            var fixture = new PushFixture
            {
                Root = root, Url = url, App = app, Client = process, State = state,
                Hash = Convert.ToHexString(SHA256.HashData(bytes))
            };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) fixture._output.Enqueue(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) fixture._output.Enqueue(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                if (credentialSetting == null)
                    await fixture.WaitForFileAsync("client-ready");
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
            return fixture;
        }

        public PushPayload Packet(AppType type, int id) => new()
        {
            RecordId = id, ProductId = "push-product",
            Platform = OperatingSystem.IsWindows() ? 1 : OperatingSystem.IsLinux() ? 2 : 3,
            AppType = (int)type, PackageType = 2, Format = "zip",
            Name = "package.zip", Url = Url + "/package.zip", Hash = Hash, Version = "2.0.0",
            Size = State.Zip.Length
        };

        public Task SendAsync(PushPayload packet) => App.Services.GetRequiredService<IHubContext<PushTestHub>>()
            .Clients.All.SendAsync("ReceiveMessage",
                JsonSerializer.Serialize(packet, PushPayloadJsonContext.Default.PushPayload));

        public Task WaitForFileAsync(string name) => WaitUntilAsync(() => File.Exists(Path.Combine(Root, name)));

        public async Task WaitUntilAsync(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!condition())
            {
                if (timeout.IsCancellationRequested)
                    Assert.Fail("Timed out waiting for push process: " + string.Join(Environment.NewLine, _output));
                await Task.Delay(50);
            }
        }

        public async ValueTask DisposeAsync()
        {
            File.WriteAllText(Path.Combine(Root, "client-stop"), "");
            File.WriteAllText(Path.Combine(Root, "resume-apply"), "");
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Client.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Client.Kill(entireProcessTree: true);
                await Client.WaitForExitAsync();
            }
            Client.Dispose();
            await App.DisposeAsync();
            Directory.Delete(Root, recursive: true);
        }
    }

    public sealed class PushTestHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            Assert.Equal(PushFixture.AppKey, Context.GetHttpContext()!.Request.Headers["appkey"].ToString());
            return base.OnConnectedAsync();
        }
    }

    public sealed class ServerState
    {
        public string Root { get; init; } = "";
        public byte[] Zip { get; init; } = Array.Empty<byte>();
        public int ValidationStatus = 200;
        public string ValidationBody = """{"code":200,"body":true}""";
        public TimeSpan ValidationDelay;
        public bool RedirectDownload;
        public int Downloads;
        public int Redirects;
        public int Verifications;
        public ConcurrentQueue<Validation> Validations { get; } = new();
        public ConcurrentQueue<Receipt> Reports { get; } = new();
    }

    public record Validation(int RecordId, string AppKey, string PacketHash, string Tenant, bool MarkerExists);
    public record Receipt(int RecordId, int Status, int Type, string Tenant, bool MarkerExists);
}
