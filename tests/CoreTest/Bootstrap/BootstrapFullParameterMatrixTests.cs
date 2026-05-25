using System;
using System.IO;
using System.Text;
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
        [Fact] public void AppType_OSS() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.OSS));
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
