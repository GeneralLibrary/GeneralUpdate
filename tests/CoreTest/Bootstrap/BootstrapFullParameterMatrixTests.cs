using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Full parameter matrix tests -- verifies ALL framework-level UpdateOptions
    /// can be set via .Option() without throwing. Business fields (UpdateUrl, Token, etc.)
    /// are now stored in Configinfo, not in UpdateOptions.
    /// </summary>
    public class BootstrapFullParameterMatrixTests : IDisposable
    {
        private readonly string _testDir;

        public BootstrapFullParameterMatrixTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GU_FullMatrix_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }

        private GeneralUpdateBootstrap B() => new GeneralUpdateBootstrap()
            .SetConfig(new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            });

        #region Core
        [Fact] public void AppType_Client() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.Client));
        [Fact] public void AppType_Upgrade() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.Upgrade));
        [Fact] public void AppType_OSSClient() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.OSSClient));
        [Fact] public void AppType_OSSUpgrade() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.OSSUpgrade));
        [Fact] public void DiffMode_Serial() => Assert.NotNull(B().Option(UpdateOptions.DiffMode, DiffMode.Serial));
        [Fact] public void DiffMode_Parallel() => Assert.NotNull(B().Option(UpdateOptions.DiffMode, DiffMode.Parallel));
        [Fact] public void Encoding_Utf8() => Assert.NotNull(B().Option(UpdateOptions.Encoding, Encoding.UTF8));
        [Fact] public void Encoding_Ascii() => Assert.NotNull(B().Option(UpdateOptions.Encoding, Encoding.ASCII));
        [Fact] public void Format_ZIP() => Assert.NotNull(B().Option(UpdateOptions.Format, "ZIP"));
        [Theory][InlineData(10)][InlineData(30)][InlineData(60)][InlineData(300)]
        public void DownloadTimeout_Various(int t) => Assert.NotNull(B().Option(UpdateOptions.DownloadTimeout, t));
        [Fact] public void PatchEnabled_True() => Assert.NotNull(B().Option(UpdateOptions.PatchEnabled, true));
        [Fact] public void PatchEnabled_False() => Assert.NotNull(B().Option(UpdateOptions.PatchEnabled, false));
        [Fact] public void BackupEnabled_False() => Assert.NotNull(B().Option(UpdateOptions.BackupEnabled, false));
        [Fact] public void Silent_True() => Assert.NotNull(B().Option(UpdateOptions.Silent, true));
        #endregion

        #region Silent
        [Fact] public void SilentAutoInstall_True() => Assert.NotNull(B().Option(UpdateOptions.SilentAutoInstall, true));
        [Theory][InlineData(15)][InlineData(30)][InlineData(60)]
        public void SilentPollInterval_Various(int m) => Assert.NotNull(B().Option(UpdateOptions.SilentPollIntervalMinutes, m));
        #endregion

        #region Download Performance
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)][InlineData(10)]
        public void MaxConcurrency_Various(int c) => Assert.NotNull(B().Option(UpdateOptions.MaxConcurrency, c));
        [Fact] public void EnableResume_False() => Assert.NotNull(B().Option(UpdateOptions.EnableResume, false));
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)]
        public void RetryCount_Various(int c) => Assert.NotNull(B().Option(UpdateOptions.RetryCount, c));
        [Fact] public void VerifyChecksum_False() => Assert.NotNull(B().Option(UpdateOptions.VerifyChecksum, false));
        [Fact] public void RetryInterval_Custom() => Assert.NotNull(B().Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(3)));
        #endregion

        #region Blacklist/Misc
        [Fact] public void Hub_Configured() => Assert.NotNull(B().Option(UpdateOptions.Hub,
            new HubConfig { Url = "https://signalr.example.com/hub" }));
        #endregion

        #region Extension Injection — Hooks / Strategy / Policy / Differ / Pipeline / etc.

        private sealed class StubHooks : GeneralUpdate.Core.Hooks.IUpdateHooks
        {
            public Task<bool> OnBeforeUpdateAsync(GeneralUpdate.Core.Hooks.UpdateContext ctx) => Task.FromResult(true);
            public Task OnDownloadCompletedAsync(GeneralUpdate.Core.Hooks.DownloadContext ctx) => Task.CompletedTask;
            public Task OnAfterUpdateAsync(GeneralUpdate.Core.Hooks.UpdateContext ctx) => Task.CompletedTask;
            public Task OnUpdateErrorAsync(GeneralUpdate.Core.Hooks.UpdateContext ctx, Exception ex) => Task.CompletedTask;
            public Task OnBeforeStartAppAsync(GeneralUpdate.Core.Hooks.UpdateContext ctx) => Task.CompletedTask;
        }

        private sealed class StubStrategy : GeneralUpdate.Core.Strategy.IStrategy
        {
            public void Create(GlobalConfigInfo parameter) { }
            public void Execute() { }
            public Task ExecuteAsync() => Task.CompletedTask;
            public void StartApp() { }
        }

        private sealed class StubSslPolicy : GeneralUpdate.Core.Security.ISslValidationPolicy
        {
            public bool ValidateCertificate(System.Security.Cryptography.X509Certificates.X509Certificate2? certificate,
                System.Security.Cryptography.X509Certificates.X509Chain? chain,
                System.Net.Security.SslPolicyErrors sslPolicyErrors) => true;
        }

        private sealed class StubBinaryDiffer : GeneralUpdate.Core.Differential.IBinaryDiffer
        {
            public Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class StubPipelineFactory : GeneralUpdate.Core.Pipeline.IUpdatePipelineFactory
        {
            public Task ExecutePipelineAsync(GeneralUpdate.Core.Pipeline.PipelineContext context, CancellationToken token = default) => Task.CompletedTask;
        }

        private sealed class StubDownloadPolicy : GeneralUpdate.Core.Download.Abstractions.IDownloadPolicy
        {
            public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default) => action(token);
        }

        private sealed class StubDownloadExecutor : GeneralUpdate.Core.Download.Abstractions.IDownloadExecutor
        {
            public Task<GeneralUpdate.Core.Download.Models.DownloadResult> ExecuteAsync(string url, string destPath,
                IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>? progress = null, CancellationToken token = default)
                => Task.FromResult(new GeneralUpdate.Core.Download.Models.DownloadResult(url, destPath, 0, TimeSpan.Zero, 0, true, null));
        }

        private sealed class StubDownloadSource : GeneralUpdate.Core.Download.Abstractions.IDownloadSource
        {
            public Task<IReadOnlyList<GeneralUpdate.Core.Download.Models.DownloadAsset>> ListAsync(CancellationToken token = default)
                => Task.FromResult<IReadOnlyList<GeneralUpdate.Core.Download.Models.DownloadAsset>>(Array.Empty<GeneralUpdate.Core.Download.Models.DownloadAsset>());
        }

        private sealed class StubDownloadPipeline : GeneralUpdate.Core.Download.Abstractions.IDownloadPipeline
        {
            public Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default) => Task.FromResult("");
        }

        private sealed class StubUpdateReporter : GeneralUpdate.Core.Download.Reporting.IUpdateReporter
        {
            public Task ReportAsync(GeneralUpdate.Core.Download.Reporting.UpdateReport report, CancellationToken token = default) => Task.CompletedTask;
        }

        private sealed class StubUpdateAuth : GeneralUpdate.Core.Security.IHttpAuthProvider
        {
            public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken token = default) => Task.CompletedTask;
        }

        private sealed class StubDownloadOrchestrator : GeneralUpdate.Core.Download.Abstractions.IDownloadOrchestrator
        {
            public Task<GeneralUpdate.Core.Download.Abstractions.DownloadReport> ExecuteAsync(
                GeneralUpdate.Core.Download.Models.DownloadPlan plan, string destDir, int maxConcurrency = 3,
                IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>? progress = null, CancellationToken token = default)
                => Task.FromResult(new GeneralUpdate.Core.Download.Abstractions.DownloadReport(Array.Empty<GeneralUpdate.Core.Download.Models.DownloadResult>(), 0, TimeSpan.Zero, 0, 0));
        }

        private sealed class StubCleanStrategy : GeneralUpdate.Core.Differential.ICleanStrategy
        {
            public Task ExecuteAsync(string sourcePath, string targetPath, string patchPath) => Task.CompletedTask;
        }

        private sealed class StubDirtyStrategy : GeneralUpdate.Core.Differential.IDirtyStrategy
        {
            public Task ExecuteAsync(string appPath, string patchPath) => Task.CompletedTask;
        }

        [Fact] public void Inject_Hooks() => Assert.NotNull(B().Hooks<StubHooks>());
        [Fact] public void Inject_Strategy() => Assert.NotNull(B().Strategy<StubStrategy>());
        [Fact] public void Inject_SslPolicy() => Assert.NotNull(B().SslPolicy<StubSslPolicy>());
        [Fact] public void Inject_BinaryDiffer() => Assert.NotNull(B().BinaryDiffer<StubBinaryDiffer>());
        [Fact] public void Inject_PipelineFactory() => Assert.NotNull(B().PipelineFactory<StubPipelineFactory>());
        [Fact] public void Inject_DownloadPolicy() => Assert.NotNull(B().DownloadPolicy<StubDownloadPolicy>());
        [Fact] public void Inject_DownloadExecutor() => Assert.NotNull(B().DownloadExecutor<StubDownloadExecutor>());
        [Fact] public void Inject_DownloadSource() => Assert.NotNull(B().DownloadSource<StubDownloadSource>());
        [Fact] public void Inject_DownloadPipeline() => Assert.NotNull(B().DownloadPipeline<StubDownloadPipeline>());
        [Fact] public void Inject_UpdateReporter() => Assert.NotNull(B().UpdateReporter<StubUpdateReporter>());
        [Fact] public void Inject_UpdateAuth() => Assert.NotNull(B().UpdateAuth<StubUpdateAuth>());
        [Fact] public void Inject_DownloadOrchestrator() => Assert.NotNull(B().DownloadOrchestrator<StubDownloadOrchestrator>());
        [Fact] public void Inject_CleanStrategy() => Assert.NotNull(B().CleanStrategy<StubCleanStrategy>());
        [Fact] public void Inject_DirtyStrategy() => Assert.NotNull(B().DirtyStrategy<StubDirtyStrategy>());

        [Fact]
        public void Chain_AllExtensionsInjected()
        {
            var b = B()
                .Hooks<StubHooks>()
                .UpdateReporter<StubUpdateReporter>()
                .DownloadPolicy<StubDownloadPolicy>()
                .DownloadExecutor<StubDownloadExecutor>()
                .DownloadSource<StubDownloadSource>()
                .DownloadPipeline<StubDownloadPipeline>()
                .DownloadOrchestrator<StubDownloadOrchestrator>()
                .BinaryDiffer<StubBinaryDiffer>()
                .CleanStrategy<StubCleanStrategy>()
                .DirtyStrategy<StubDirtyStrategy>()
                .SslPolicy<StubSslPolicy>()
                .UpdateAuth<StubUpdateAuth>()
                .PipelineFactory<StubPipelineFactory>()
                .Strategy<StubStrategy>();
            Assert.NotNull(b);
        }
        #endregion

        #region Full Combination Chains
        [Fact] public void Chain_AllFrameworkOptions()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Client)
                .Option(UpdateOptions.DiffMode, DiffMode.Parallel)
                .Option(UpdateOptions.Encoding, Encoding.UTF8)
                .Option(UpdateOptions.Format, "ZIP")
                .Option(UpdateOptions.DownloadTimeout, 120)
                .Option(UpdateOptions.PatchEnabled, true)
                .Option(UpdateOptions.BackupEnabled, true)
                .Option(UpdateOptions.Silent, false)
                .Option(UpdateOptions.MaxConcurrency, 4)
                .Option(UpdateOptions.EnableResume, true)
                .Option(UpdateOptions.RetryCount, 5)
                .Option(UpdateOptions.VerifyChecksum, true)
                .Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(2))
                .Option(UpdateOptions.SilentAutoInstall, false)
                .Option(UpdateOptions.SilentPollIntervalMinutes, 30)
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://update.example.com/api/v2",
                    MainAppName = "MyProduct.exe",
                    ClientVersion = "1.0.0",
                    InstallPath = _testDir,
                    AppSecretKey = "secret-key-2026",
                    Scheme = "Bearer",
                    Token = "jwt-token-xyz"
                });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_SilentClient()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Client)
                .Option(UpdateOptions.Silent, true)
                .Option(UpdateOptions.SilentAutoInstall, true)
                .Option(UpdateOptions.SilentPollIntervalMinutes, 15)
                .SetConfig(new Configinfo { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_ParallelDiffHighConcurrency()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.DiffMode, DiffMode.Parallel).Option(UpdateOptions.MaxConcurrency, 8)
                .SetConfig(new Configinfo { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_UpgradeNoBackup()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Upgrade).Option(UpdateOptions.BackupEnabled, false)
                .Option(UpdateOptions.PatchEnabled, true).Option(UpdateOptions.VerifyChecksum, true)
                .SetConfig(new Configinfo { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_ClientAndUpgrade_BothFullyConfigured()
        {
            var sharedConfig = new Configinfo
            {
                UpdateUrl = "https://update.enterprise.com/api/v2",
                AppName = "Update.exe", MainAppName = "EnterpriseApp.exe",
                ClientVersion = "4.2.1", UpgradeClientVersion = "2.0.0",
                InstallPath = _testDir, AppSecretKey = "enterprise-prod-key-2026",
                ProductId = "enterprise-app-v4",
                UpdateLogUrl = "https://enterprise.com/releases",
                ReportUrl = "https://telemetry.enterprise.com/api/report",
                Scheme = "HMAC", Token = "hmac-prod-secret", Bowl = "Bowl.exe",
                BlackFiles = new List<string> { "*.pdb" },
                BlackFormats = new List<string> { ".log", ".tmp" },
                SkipDirectorys = new List<string> { "logs", "temp" }
            };

            var client = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Client)
                .Option(UpdateOptions.DiffMode, DiffMode.Parallel)
                .Option(UpdateOptions.Encoding, Encoding.UTF8)
                .Option(UpdateOptions.Format, "ZIP")
                .Option(UpdateOptions.DownloadTimeout, 120)
                .Option(UpdateOptions.PatchEnabled, true)
                .Option(UpdateOptions.BackupEnabled, true)
                .Option(UpdateOptions.Silent, false)
                .Option(UpdateOptions.MaxConcurrency, 4)
                .Option(UpdateOptions.EnableResume, true)
                .Option(UpdateOptions.RetryCount, 5)
                .Option(UpdateOptions.VerifyChecksum, true)
                .Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(2))
                .SetConfig(sharedConfig)
                .AddListenerUpdatePrecheck(args =>
                {
                    var hour = DateTime.Now.Hour;
                    return hour < 2 || hour > 6;
                })
                .AddListenerUpdateInfo((s, e) => { })
                .AddListenerMultiAllDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadError((s, e) => { })
                .AddListenerMultiDownloadStatistics((s, e) => { })
                .AddListenerException((s, e) => { });
            Assert.NotNull(client);

            var upgrade = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Upgrade)
                .Option(UpdateOptions.DiffMode, DiffMode.Parallel)
                .Option(UpdateOptions.Encoding, Encoding.UTF8)
                .Option(UpdateOptions.Format, "ZIP")
                .Option(UpdateOptions.DownloadTimeout, 30)
                .Option(UpdateOptions.PatchEnabled, true)
                .Option(UpdateOptions.BackupEnabled, false)
                .Option(UpdateOptions.MaxConcurrency, 2)
                .Option(UpdateOptions.VerifyChecksum, true)
                .Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(1))
                .SetConfig(sharedConfig)
                .AddListenerException((s, e) => { })
                .AddListenerUpdateInfo((s, e) => { });
            Assert.NotNull(upgrade);
            Assert.NotSame(client, upgrade);
        }
        #endregion
    }
}
