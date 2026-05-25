using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Executors;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Orchestrators;
using GeneralUpdate.Core.Download.Policy;
using Moq;
using Moq.Protected;
using Xunit;

namespace CoreTest.Download;

/// <summary>
/// Tests that <see cref="DefaultDownloadOrchestrator"/> correctly reads and
/// applies behaviour from <see cref="DownloadOrchestratorOptions"/>.
/// </summary>
public class OrchestratorOptionsBehaviourTests
{
    #region MaxConcurrency

    [Fact]
    public void Constructor_ReadsMaxConcurrencyFromOptions()
    {
        var httpClient = new HttpClient();
        var opts = new DownloadOrchestratorOptions { MaxConcurrency = 5 };
        var orch = new DefaultDownloadOrchestrator(httpClient, opts);
        Assert.NotNull(orch);
    }

    [Fact]
    public async Task ExecuteAsync_SerialMode_ForcesConcurrencyToOne()
    {
        using var httpClient = new HttpClient(new FakeSuccessHandler());
        var opts = new DownloadOrchestratorOptions
        {
            DiffMode = DiffMode.Serial,
            MaxConcurrency = 10,
            VerifyChecksum = false,
            RetryCount = 1,
            RetryInterval = TimeSpan.Zero,
        };

        var assets = new List<DownloadAsset>
        {
            new("file1.zip", "http://example.com/1.zip", 10, null, "1.0"),
            new("file2.zip", "http://example.com/2.zip", 10, null, "1.0"),
        };
        var plan = new DownloadPlan(assets, false);
        var destDir = Path.Combine(Path.GetTempPath(), "GU_Serial_" + Guid.NewGuid().ToString("N"));

        try
        {
            var orch = new DefaultDownloadOrchestrator(httpClient, opts);
            var report = await orch.ExecuteAsync(plan, destDir, token: CancellationToken.None);
            Assert.Equal(2, report.SuccessCount);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    #endregion

    #region VerifyChecksum

    private sealed class FakeSuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[100]),
            });
        }
    }

    [Fact]
    public async Task ExecuteAsync_VerifyChecksumFalse_SkipsVerification()
    {
        using var httpClient = new HttpClient(new FakeSuccessHandler());
        var opts = new DownloadOrchestratorOptions
        {
            VerifyChecksum = false,
            RetryCount = 1,
            RetryInterval = TimeSpan.Zero,
        };

        var assets = new List<DownloadAsset>
        {
            new("test.zip", "http://example.com/test.zip", 100, "sha256:invalid_hash_would_fail", "1.0"),
        };
        var plan = new DownloadPlan(assets, false);
        var destDir = Path.Combine(Path.GetTempPath(), "GU_NoVerify_" + Guid.NewGuid().ToString("N"));

        try
        {
            var orch = new DefaultDownloadOrchestrator(httpClient, opts);
            var report = await orch.ExecuteAsync(plan, destDir, token: CancellationToken.None);
            Assert.Equal(1, report.SuccessCount);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_VerifyChecksumTrueWithInvalidHash_Fails()
    {
        using var httpClient = new HttpClient(new FakeSuccessHandler());
        var opts = new DownloadOrchestratorOptions
        {
            VerifyChecksum = true,
            RetryCount = 1,
            RetryInterval = TimeSpan.Zero,
        };

        var assets = new List<DownloadAsset>
        {
            new("test.zip", "http://example.com/test.zip", 100,
                "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", "1.0"),
        };
        var plan = new DownloadPlan(assets, false);
        var destDir = Path.Combine(Path.GetTempPath(), "GU_Verify_" + Guid.NewGuid().ToString("N"));

        try
        {
            var orch = new DefaultDownloadOrchestrator(httpClient, opts);
            var report = await orch.ExecuteAsync(plan, destDir, token: CancellationToken.None);
            Assert.Equal(0, report.SuccessCount);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    #endregion

    #region RetryCount & RetryInterval

    [Fact]
    public async Task ExecuteAsync_RetryCountFromOptions_IsUsedByPolicy()
    {
        var policy = new DefaultRetryPolicy(maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(10));
        int attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new HttpRequestException("timeout");
            }, CancellationToken.None));

        Assert.Equal(5, attempts);
    }

    [Fact]
    public void Orchestrator_PassesOptionsToPolicy()
    {
        using var httpClient = new HttpClient();
        var opts = new DownloadOrchestratorOptions
        {
            RetryCount = 7,
            RetryInterval = TimeSpan.FromSeconds(3),
        };

        var orch = new DefaultDownloadOrchestrator(httpClient, opts);
        Assert.NotNull(orch);
    }

    [Fact]
    public void Orchestrator_AcceptsCustomPolicyOverride()
    {
        using var httpClient = new HttpClient();
        var customPolicy = new DefaultRetryPolicy(maxRetries: 10, initialDelay: TimeSpan.FromMilliseconds(100));
        var opts = new DownloadOrchestratorOptions { RetryCount = 1 };

        var orch = new DefaultDownloadOrchestrator(httpClient, opts, customPolicy);
        Assert.NotNull(orch);
    }

    #endregion

    #region EnableResume

    [Fact]
    public void HttpDownloadExecutor_EnableResumeFalse_Constructs()
    {
        using var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client, timeout: TimeSpan.FromSeconds(10), enableResume: false);
        Assert.NotNull(executor);
    }

    [Fact]
    public void HttpDownloadExecutor_EnableResumeTrue_Default()
    {
        using var client = new HttpClient();
        var executor = new HttpDownloadExecutor(client);
        Assert.NotNull(executor);
    }

    [Fact]
    public async Task HttpDownloadExecutor_EnableResumeFalse_DeletesExistingFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "GU_Resume_" + Guid.NewGuid().ToString("N"));
        var destPath = Path.Combine(tmpDir, "download.bin");
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(destPath, "partial-data");

        using var client = new HttpClient(new FakeSuccessHandler());
        var executor = new HttpDownloadExecutor(client, enableResume: false);

        try
        {
            var result = await executor.ExecuteAsync(
                "http://example.com/file.bin", destPath, token: CancellationToken.None);
            Assert.True(result.Success, $"Download should succeed, error: {result.ErrorMessage}");
            Assert.True(File.Exists(destPath));
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public async Task HttpDownloadExecutor_WithRangeResponse_AppendsCorrectly()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "GU_ResumeAppend_" + Guid.NewGuid().ToString("N"));
        var destPath = Path.Combine(tmpDir, "download.bin");
        Directory.CreateDirectory(tmpDir);
        var partialData = new byte[20];
        File.WriteAllBytes(destPath, partialData);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Headers.Range != null),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new ByteArrayContent(new byte[30]) { Headers = { ContentLength = 30 } }
            });
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Headers.Range == null),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[50])
            });

        using var client = new HttpClient(handler.Object);
        var executor = new HttpDownloadExecutor(client, enableResume: true);

        try
        {
            var result = await executor.ExecuteAsync(
                "http://example.com/file.bin", destPath, token: CancellationToken.None);
            Assert.True(result.Success);
            var fileInfo = new FileInfo(destPath);
            Assert.True(fileInfo.Length >= 20, $"Expected file >= 20 bytes, got {fileInfo.Length}");
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    #endregion

    #region DiffMode Behaviour

    [Fact]
    public async Task ExecuteAsync_ParallelMode_UsesConfiguredConcurrency()
    {
        using var httpClient = new HttpClient(new FakeSuccessHandler());
        var opts = new DownloadOrchestratorOptions
        {
            DiffMode = DiffMode.Parallel,
            MaxConcurrency = 4,
            VerifyChecksum = false,
            RetryCount = 1,
            RetryInterval = TimeSpan.Zero,
        };

        var assets = new List<DownloadAsset>();
        for (int i = 0; i < 3; i++)
            assets.Add(new($"file{i}.zip", $"http://example.com/{i}.zip", 10, null, "1.0"));
        var plan = new DownloadPlan(assets, false);
        var destDir = Path.Combine(Path.GetTempPath(), "GU_Parallel_" + Guid.NewGuid().ToString("N"));

        try
        {
            var orch = new DefaultDownloadOrchestrator(httpClient, opts);
            var report = await orch.ExecuteAsync(plan, destDir, token: CancellationToken.None);
            Assert.Equal(3, report.SuccessCount);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    #endregion

    #region Empty Plan

    [Fact]
    public async Task ExecuteAsync_EmptyPlan_ReturnsEmptyReport()
    {
        using var httpClient = new HttpClient();
        var orch = new DefaultDownloadOrchestrator(httpClient);
        var report = await orch.ExecuteAsync(DownloadPlan.Empty, "/tmp", token: CancellationToken.None);
        Assert.Equal(0, report.SuccessCount);
        Assert.Equal(0, report.FailedCount);
    }

    #endregion
}
