using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Hooks;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Full parameter matrix tests -- verifies ALL framework-level Option
    /// can be set via .SetOption() without throwing. Business fields (UpdateUrl, Token, etc.)
    /// are now stored in UpdateRequest, not in Option.
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
            .SetConfig(new UpdateRequest
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
        [Fact] public void AppType_Client() => Assert.NotNull(B().SetOption(Option.AppType, AppType.Client));
        [Fact] public void AppType_Upgrade() => Assert.NotNull(B().SetOption(Option.AppType, AppType.Upgrade));
        [Fact] public void AppType_OssClient() => Assert.NotNull(B().SetOption(Option.AppType, AppType.OssClient));
        [Fact] public void AppType_OssUpgrade() => Assert.NotNull(B().SetOption(Option.AppType, AppType.OssUpgrade));
        [Fact] public void DiffMode_Serial() => Assert.NotNull(B().SetOption(Option.DiffMode, DiffMode.Serial));
        [Fact] public void DiffMode_Parallel() => Assert.NotNull(B().SetOption(Option.DiffMode, DiffMode.Parallel));
        [Fact] public void Encoding_Utf8() => Assert.NotNull(B().SetOption(Option.Encoding, Encoding.UTF8));
        [Fact] public void Encoding_Ascii() => Assert.NotNull(B().SetOption(Option.Encoding, Encoding.ASCII));
        [Fact] public void Format_ZIP() => Assert.NotNull(B().SetOption(Option.Format, Format.Zip));
        [Theory][InlineData(10)][InlineData(30)][InlineData(60)][InlineData(300)]
        public void DownloadTimeout_Various(int t) => Assert.NotNull(B().SetOption(Option.DownloadTimeout, t));
        [Fact] public void PatchEnabled_True() => Assert.NotNull(B().SetOption(Option.PatchEnabled, true));
        [Fact] public void PatchEnabled_False() => Assert.NotNull(B().SetOption(Option.PatchEnabled, false));
        [Fact] public void BackupEnabled_False() => Assert.NotNull(B().SetOption(Option.BackupEnabled, false));
        [Fact] public void Silent_True() => Assert.NotNull(B().SetOption(Option.Silent, true));
        #endregion

        #region Silent
        [Theory][InlineData(15)][InlineData(30)][InlineData(60)]
        public void SilentPollInterval_Various(int m) => Assert.NotNull(B().SetOption(Option.SilentPollIntervalMinutes, m));
        #endregion

        #region Download Performance
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)][InlineData(10)]
        public void MaxConcurrency_Various(int c) => Assert.NotNull(B().SetOption(Option.MaxConcurrency, c));
        [Fact] public void EnableResume_False() => Assert.NotNull(B().SetOption(Option.EnableResume, false));
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)]
        public void RetryCount_Various(int c) => Assert.NotNull(B().SetOption(Option.RetryCount, c));
        [Fact] public void VerifyChecksum_False() => Assert.NotNull(B().SetOption(Option.VerifyChecksum, false));
        [Fact] public void RetryInterval_Custom() => Assert.NotNull(B().SetOption(Option.RetryInterval, TimeSpan.FromSeconds(3)));
        #endregion

        #region Blacklist/Misc
        #endregion

        #region Extension Injection — Hooks / Strategy / Policy / Differ / Pipeline / etc.

        private sealed class StubHooks : GeneralUpdate.Core.Hooks.IUpdateHooks
        {
            public Task<bool> OnBeforeUpdateAsync(GeneralUpdate.Core.Hooks.HookContext ctx) => Task.FromResult(true);
            public Task OnDownloadCompletedAsync(GeneralUpdate.Core.Hooks.DownloadContext ctx) => Task.CompletedTask;
            public Task OnAfterUpdateAsync(GeneralUpdate.Core.Hooks.HookContext ctx) => Task.CompletedTask;
            public Task OnUpdateErrorAsync(GeneralUpdate.Core.Hooks.HookContext ctx, Exception ex) => Task.CompletedTask;
            public Task OnBeforeStartAppAsync(GeneralUpdate.Core.Hooks.HookContext ctx) => Task.CompletedTask;
        }

        private sealed class StubStrategy : GeneralUpdate.Core.Strategy.IStrategy
        {
            public void Create(UpdateContext parameter) { }
            public IUpdateHooks Hooks { get; set; }
            public IUpdateReporter Reporter { get; set; }
            public Task ExecuteAsync() => Task.CompletedTask;
            public Task StartAppAsync() => Task.CompletedTask;
        }

        private sealed class StubSslPolicy : GeneralUpdate.Core.Security.ISslValidationPolicy
        {
            public bool ValidateCertificate(System.Security.Cryptography.X509Certificates.X509Certificate2? certificate,
                System.Security.Cryptography.X509Certificates.X509Chain? chain,
                System.Net.Security.SslPolicyErrors sslPolicyErrors) => true;
        }


        private sealed class StubDownloadPolicy : GeneralUpdate.Core.Download.Abstractions.IDownloadPolicy
        {
            public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default) => action(token);
        }

        private sealed class StubDownloadExecutor : GeneralUpdate.Core.Download.Abstractions.IDownloadExecutor
        {
            public Task<GeneralUpdate.Core.Download.Models.DownloadResult> ExecuteAsync(GeneralUpdate.Core.Download.Models.DownloadAsset asset, string destPath,
                IProgress<GeneralUpdate.Core.Download.Models.DownloadProgress>? progress = null, CancellationToken token = default)
                => Task.FromResult(new GeneralUpdate.Core.Download.Models.DownloadResult(asset, destPath, 0, TimeSpan.Zero, 0, true, null));
        }

        private sealed class StubDownloadSource : GeneralUpdate.Core.Download.Abstractions.IDownloadSource
        {
            public Task<GeneralUpdate.Core.Download.Models.DownloadSourceResult> ListAsync(CancellationToken token = default)
                => Task.FromResult(new GeneralUpdate.Core.Download.Models.DownloadSourceResult
                {
                    Assets = Array.Empty<GeneralUpdate.Core.Download.Models.DownloadAsset>()
                });
        }

        private sealed class StubDownloadPipeline : GeneralUpdate.Core.Download.Abstractions.IDownloadPipeline
        {
            public Task<string> ProcessAsync(string downloadedPath, CancellationToken token = default) => Task.FromResult("");
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

        [Fact] public void Inject_Hooks() => Assert.NotNull(B().Hooks<StubHooks>());
        [Fact] public void Inject_Strategy() => Assert.NotNull(B().Strategy<StubStrategy>());
        [Fact] public void Inject_SslPolicy() => Assert.NotNull(B().SslPolicy<StubSslPolicy>());
        [Fact] public void Inject_DownloadPolicy() => Assert.NotNull(B().DownloadPolicy<StubDownloadPolicy>());
        [Fact] public void Inject_DownloadExecutor() => Assert.NotNull(B().DownloadExecutor<StubDownloadExecutor>());
        [Fact] public void Inject_DownloadSource() => Assert.NotNull(B().DownloadSource<StubDownloadSource>());
        [Fact] public void Inject_DownloadPipeline() => Assert.NotNull(B().DownloadPipeline<StubDownloadPipeline>());
        [Fact] public void Inject_DownloadOrchestrator() => Assert.NotNull(B().DownloadOrchestrator<StubDownloadOrchestrator>());

        [Fact]
        public void Chain_AllExtensionsInjected()
        {
            var b = B()
                .Hooks<StubHooks>()
                .DownloadPolicy<StubDownloadPolicy>()
                .DownloadExecutor<StubDownloadExecutor>()
                .DownloadSource<StubDownloadSource>()
                .DownloadPipeline<StubDownloadPipeline>()
                .DownloadOrchestrator<StubDownloadOrchestrator>()
                .SslPolicy<StubSslPolicy>()
                .HttpAuth<StubUpdateAuth>()
                .Strategy<StubStrategy>();
            Assert.NotNull(b);
        }
        #endregion

        #region Full Combination Chains
        [Fact] public void Chain_AllFrameworkOptions()
        {
            var b = new GeneralUpdateBootstrap()
                .SetOption(Option.AppType, AppType.Client)
                .SetOption(Option.DiffMode, DiffMode.Parallel)
                .SetOption(Option.Encoding, Encoding.UTF8)
                .SetOption(Option.Format, Format.Zip)
                .SetOption(Option.DownloadTimeout, 120)
                .SetOption(Option.PatchEnabled, true)
                .SetOption(Option.BackupEnabled, true)
                .SetOption(Option.Silent, false)
                .SetOption(Option.MaxConcurrency, 4)
                .SetOption(Option.EnableResume, true)
                .SetOption(Option.RetryCount, 5)
                .SetOption(Option.VerifyChecksum, true)
                .SetOption(Option.RetryInterval, TimeSpan.FromSeconds(2))
                .SetOption(Option.SilentPollIntervalMinutes, 30)
                .SetConfig(new UpdateRequest
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
                .SetOption(Option.AppType, AppType.Client)
                .SetOption(Option.Silent, true)
                .SetOption(Option.SilentPollIntervalMinutes, 15)
                .SetConfig(new UpdateRequest { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_ParallelDiffHighConcurrency()
        {
            var b = new GeneralUpdateBootstrap()
                .SetOption(Option.DiffMode, DiffMode.Parallel).SetOption(Option.MaxConcurrency, 8)
                .SetConfig(new UpdateRequest { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_UpgradeNoBackup()
        {
            var b = new GeneralUpdateBootstrap()
                .SetOption(Option.AppType, AppType.Upgrade).SetOption(Option.BackupEnabled, false)
                .SetOption(Option.PatchEnabled, true).SetOption(Option.VerifyChecksum, true)
                .SetConfig(new UpdateRequest { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "https", Token = "token" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_ClientAndUpgrade_BothFullyConfigured()
        {
            var sharedConfig = new UpdateRequest
            {
                UpdateUrl = "https://update.enterprise.com/api/v2",
                UpdateAppName = "Update.exe", MainAppName = "EnterpriseApp.exe",
                ClientVersion = "4.2.1", UpgradeClientVersion = "2.0.0",
                InstallPath = _testDir, AppSecretKey = "enterprise-prod-key-2026",
                ProductId = "enterprise-app-v4",
                UpdateLogUrl = "https://enterprise.com/releases",
                ReportUrl = "https://telemetry.enterprise.com/api/report",
                Scheme = "HMAC", Token = "hmac-prod-secret", Bowl = "Bowl.exe",
                Files = new List<string> { "*.pdb" },
                Formats = new List<string> { ".log", ".tmp" },
                Directories = new List<string> { "logs", "temp" }
            };

            var client = new GeneralUpdateBootstrap()
                .SetOption(Option.AppType, AppType.Client)
                .SetOption(Option.DiffMode, DiffMode.Parallel)
                .SetOption(Option.Encoding, Encoding.UTF8)
                .SetOption(Option.Format, Format.Zip)
                .SetOption(Option.DownloadTimeout, 120)
                .SetOption(Option.PatchEnabled, true)
                .SetOption(Option.BackupEnabled, true)
                .SetOption(Option.Silent, false)
                .SetOption(Option.MaxConcurrency, 4)
                .SetOption(Option.EnableResume, true)
                .SetOption(Option.RetryCount, 5)
                .SetOption(Option.VerifyChecksum, true)
                .SetOption(Option.RetryInterval, TimeSpan.FromSeconds(2))
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
                .SetOption(Option.AppType, AppType.Upgrade)
                .SetOption(Option.DiffMode, DiffMode.Parallel)
                .SetOption(Option.Encoding, Encoding.UTF8)
                .SetOption(Option.Format, Format.Zip)
                .SetOption(Option.DownloadTimeout, 30)
                .SetOption(Option.PatchEnabled, true)
                .SetOption(Option.BackupEnabled, false)
                .SetOption(Option.MaxConcurrency, 2)
                .SetOption(Option.VerifyChecksum, true)
                .SetOption(Option.RetryInterval, TimeSpan.FromSeconds(1))
                .SetConfig(sharedConfig)
                .AddListenerException((s, e) => { })
                .AddListenerUpdateInfo((s, e) => { });
            Assert.NotNull(upgrade);
            Assert.NotSame(client, upgrade);
        }
        #endregion
    }
}
