using System.Net;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.Download.Reporting;

namespace CoreTest.Diagnostics;

public sealed class UpdateFailureOutboxTests
{
    [Fact]
    public void PackageWithoutRecordUsesActiveIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-update-outbox-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new UpdateContext
            {
                InstallPath = Path.Combine(root, "install"), DiagnosticsDirectory = Path.Combine(root, "state"),
                UpdateVersions = [new VersionEntry { RecordId = 99 }]
            };
            using var attempt = UpdateAttempt.Begin(config, "client");
            attempt.SetReportIdentity(42, 2);
            attempt.SetPackage(new VersionEntry { Name = "package.zip" });
            attempt.RecordFailure(new InvalidOperationException("test failure"));
            var id = InstallationIdentity.GetOrCreate(config);
            var caseDir = Assert.Single(Directory.GetDirectories(Path.Combine(config.DiagnosticsDirectory, "update-outbox", id)));
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json")));
            Assert.Equal(42, manifest.RootElement.GetProperty("recordId").GetInt32());
            Assert.Equal(2, manifest.RootElement.GetProperty("recordType").GetInt32());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [GeneralSpaceFact]
    public async Task FailureOutboxUploadsToRealGeneralSpace()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-real-update-outbox-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var e2e = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")!));
            var appKey = e2e.RootElement.GetProperty("appKey").GetString()!;
            var recordId = e2e.RootElement.GetProperty("recordId").GetInt32();
            var config = new UpdateContext
            {
                InstallPath = Path.Combine(root, "install"), DiagnosticsDirectory = Path.Combine(root, "state"),
                AppSecretKey = appKey, UpdateVersions = [new VersionEntry { RecordId = recordId }]
            };
            using var attempt = UpdateAttempt.Begin(config, "update");
            attempt.Record("filesApplying");
            attempt.RecordFailure(new InvalidOperationException("test failure"));
            var id = InstallationIdentity.GetOrCreate(config);
            var caseDir = Assert.Single(Directory.GetDirectories(Path.Combine(config.DiagnosticsDirectory, "update-outbox", id)));
            var url = new Uri(new Uri(e2e.RootElement.GetProperty("apiBaseUrl").GetString()!),
                e2e.RootElement.GetProperty("artifactEndpoint").GetString()!).ToString();
            var options = new DiagnosticsOptions { Enabled = true, Url = url, AppKey = appKey, MaxRetry = 0 };
            var scanner = new PendingDiagnosticsScanner(config.InstallPath, options,
                new DiagnosticsUploader(options), stateRoot: config.DiagnosticsDirectory, installationId: id);
            await scanner.ScanAsync(CancellationToken.None);
            Assert.False(Directory.Exists(caseDir), "Real GeneralSpace did not acknowledge the diagnostic artifact.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FailureIsScopedRedactedRetriedAndDeletedAfterAcknowledgment()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-update-outbox-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new UpdateContext
            {
                InstallPath = Path.Combine(root, "install"), DiagnosticsDirectory = Path.Combine(root, "state"),
                AppSecretKey = "sensitive-app-key", Token = "sensitive-token",
                UpdateVersions = [new VersionEntry { RecordId = 42 }]
            };
            using var attempt = UpdateAttempt.Begin(config, "update");
            attempt.Record("filesApplying");
            attempt.RecordFailure(new InvalidOperationException("sensitive-app-key sensitive-token"));
            var id = InstallationIdentity.GetOrCreate(config);
            var caseDir = Assert.Single(Directory.GetDirectories(Path.Combine(config.DiagnosticsDirectory, "update-outbox", id)));
            foreach (var file in Directory.GetFiles(caseDir))
            {
                var content = File.ReadAllText(file);
                Assert.DoesNotContain(config.AppSecretKey, content);
                Assert.DoesNotContain(config.Token, content);
            }

            var handler = new ArtifactHandler();
            var options = new DiagnosticsOptions { Enabled = true, Url = "https://example.test/Fault/UploadArtifact",
                AppKey = config.AppSecretKey, MaxRetry = 1 };
            var scanner = new PendingDiagnosticsScanner(config.InstallPath, options,
                new DiagnosticsUploader(options, new HttpClient(handler)), stateRoot: config.DiagnosticsDirectory,
                installationId: id);
            await new PendingDiagnosticsScanner(config.InstallPath, options,
                new DiagnosticsUploader(options, new HttpClient(handler)), stateRoot: config.DiagnosticsDirectory,
                installationId: Guid.NewGuid().ToString("D")).ScanAsync(CancellationToken.None);
            Assert.Equal(0, handler.Requests);
            File.WriteAllText(Path.Combine(caseDir, "extra.txt"), "not authorized");
            await scanner.ScanAsync(CancellationToken.None);
            Assert.Equal(0, handler.Requests);
            File.Delete(Path.Combine(caseDir, "extra.txt"));
            await scanner.ScanAsync(CancellationToken.None);
            await scanner.ScanAsync(CancellationToken.None);
            Assert.True(Directory.Exists(caseDir));
            using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseDir, "manifest.json"))))
                Assert.Equal("pending", manifest.RootElement.GetProperty("state").GetString());
            handler.Accept = true;
            await scanner.ScanAsync(CancellationToken.None);
            Assert.False(Directory.Exists(caseDir));
            Assert.Equal(3, handler.Requests);
            Assert.Contains("sensitive-app-key", handler.LastBody);
            Assert.Contains("42", handler.LastBody);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ArtifactHandler : HttpMessageHandler
    {
        public bool Accept { get; set; }
        public int Requests { get; private set; }
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            LastBody = await request.Content!.ReadAsStringAsync(token);
            var multipart = (MultipartFormDataContent)request.Content;
            var guid = await multipart.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "guid")
                .ReadAsStringAsync(token);
            var response = $"{{\"code\":{(Accept ? 200 : 400)},\"body\":{{\"artifactId\":\"{guid}\"}}}}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    public sealed class GeneralSpaceFactAttribute : FactAttribute
    {
        public GeneralSpaceFactAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("GENERALUPDATE_E2E_CONFIG")))
                Skip = "Set GENERALUPDATE_E2E_CONFIG to a protected real GeneralSpace test configuration file.";
        }
    }
}
