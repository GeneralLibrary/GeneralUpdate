using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Full parameter matrix tests — verifies ALL UpdateOptions constants
    /// can be set via .Option() without throwing. Covers 39 options across
    /// core, deployment, silent, download, security, reporting, OSS, and
    /// blacklist categories.
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

        #region Core (9)
        [Fact] public void AppType_Client() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.Client));
        [Fact] public void AppType_Upgrade() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.Upgrade));
        [Fact] public void AppType_OSS() => Assert.NotNull(B().Option(UpdateOptions.AppType, AppType.OSS));
        [Fact] public void DiffMode_Serial() => Assert.NotNull(B().Option(UpdateOptions.DiffMode, DiffMode.Serial));
        [Fact] public void DiffMode_Parallel() => Assert.NotNull(B().Option(UpdateOptions.DiffMode, DiffMode.Parallel));
        [Fact] public void Encoding_Utf8() => Assert.NotNull(B().Option(UpdateOptions.Encoding, Encoding.UTF8));
        [Fact] public void Encoding_Ascii() => Assert.NotNull(B().Option(UpdateOptions.Encoding, Encoding.ASCII));
        [Fact] public void Format_ZIP() => Assert.NotNull(B().Option(UpdateOptions.Format, "ZIP"));
        [Theory]
        [InlineData(10)][InlineData(30)][InlineData(60)][InlineData(300)]
        public void DownloadTimeout_Various(int t) => Assert.NotNull(B().Option(UpdateOptions.DownloadTimeout, t));
        [Fact] public void DriveEnabled_True() => Assert.NotNull(B().Option(UpdateOptions.DriveEnabled, true));
        [Fact] public void DriveEnabled_False() => Assert.NotNull(B().Option(UpdateOptions.DriveEnabled, false));
        [Fact] public void PatchEnabled_True() => Assert.NotNull(B().Option(UpdateOptions.PatchEnabled, true));
        [Fact] public void PatchEnabled_False() => Assert.NotNull(B().Option(UpdateOptions.PatchEnabled, false));
        [Fact] public void BackupEnabled_False() => Assert.NotNull(B().Option(UpdateOptions.BackupEnabled, false));
        [Fact] public void Silent_True() => Assert.NotNull(B().Option(UpdateOptions.Silent, true));
        [Fact] public void Mode_Default() => Assert.NotNull(B().Option(UpdateOptions.Mode, UpdateMode.Default));
        [Fact] public void Mode_Scripts() => Assert.NotNull(B().Option(UpdateOptions.Mode, UpdateMode.Scripts));
        #endregion

        #region Deployment (9)
        [Fact] public void UpdateUrl_Custom() => Assert.NotNull(B().Option(UpdateOptions.UpdateUrl, "https://update.company.com/api"));
        [Fact] public void AppSecretKey_Custom() => Assert.NotNull(B().Option(UpdateOptions.AppSecretKey, "prod-secret"));
        [Fact] public void AppName_Custom() => Assert.NotNull(B().Option(UpdateOptions.AppName, "Update.exe"));
        [Fact] public void MainAppName_Custom() => Assert.NotNull(B().Option(UpdateOptions.MainAppName, "ProductApp.exe"));
        [Fact] public void InstallPath_Custom() => Assert.NotNull(B().Option(UpdateOptions.InstallPath, _testDir));
        [Fact] public void ClientVersion_Custom() => Assert.NotNull(B().Option(UpdateOptions.ClientVersion, "3.1.0-beta"));
        [Fact] public void UpgradeClientVersion_Custom() => Assert.NotNull(B().Option(UpdateOptions.UpgradeClientVersion, "2.0.0"));
        [Fact] public void Platform_Windows() => Assert.NotNull(B().Option(UpdateOptions.Platform, PlatformType.Windows));
        [Fact] public void Platform_Linux() => Assert.NotNull(B().Option(UpdateOptions.Platform, PlatformType.Linux));
        #endregion

        #region Silent (2)
        [Fact] public void SilentAutoInstall_True() => Assert.NotNull(B().Option(UpdateOptions.SilentAutoInstall, true));
        [Theory][InlineData(15)][InlineData(30)][InlineData(60)]
        public void SilentPollInterval_Various(int m) => Assert.NotNull(B().Option(UpdateOptions.SilentPollIntervalMinutes, m));
        #endregion

        #region Download Performance (6)
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)][InlineData(10)]
        public void MaxConcurrency_Various(int c) => Assert.NotNull(B().Option(UpdateOptions.MaxConcurrency, c));
        [Fact] public void EnableResume_False() => Assert.NotNull(B().Option(UpdateOptions.EnableResume, false));
        [Theory][InlineData(1)][InlineData(3)][InlineData(5)]
        public void RetryCount_Various(int c) => Assert.NotNull(B().Option(UpdateOptions.RetryCount, c));
        [Fact] public void VerifyChecksum_False() => Assert.NotNull(B().Option(UpdateOptions.VerifyChecksum, false));
        [Fact] public void RetryInterval_Custom() => Assert.NotNull(B().Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(3)));
        #endregion

        #region Security (5)
        [Fact] public void Scheme_Bearer() => Assert.NotNull(B().Option(UpdateOptions.Scheme, "Bearer"));
        [Fact] public void Scheme_ApiKey() => Assert.NotNull(B().Option(UpdateOptions.Scheme, "ApiKey"));
        [Fact] public void Scheme_HMAC() => Assert.NotNull(B().Option(UpdateOptions.Scheme, "HMAC"));
        [Fact] public void Token_Custom() => Assert.NotNull(B().Option(UpdateOptions.Token, "jwt-token-xyz"));
        [Fact] public void PermissionScript_Custom() => Assert.NotNull(B().Option(UpdateOptions.PermissionScript, "#!/bin/bash\nchmod +x"));
        #endregion

        #region Reporting (3)
        [Fact] public void ReportUrl_Custom() => Assert.NotNull(B().Option(UpdateOptions.ReportUrl, "https://telemetry.example.com/report"));
        [Fact] public void ProductId_Custom() => Assert.NotNull(B().Option(UpdateOptions.ProductId, "enterprise-pro"));
        [Fact] public void UpdateLogUrl_Custom() => Assert.NotNull(B().Option(UpdateOptions.UpdateLogUrl, "https://myapp.com/releases"));
        #endregion

        #region OSS (3)
        [Fact] public void OSS_AliYun() => Assert.NotNull(B().Option(UpdateOptions.OSSProvider, OssProvider.AliYun));
        [Fact] public void OSS_AWS() => Assert.NotNull(B().Option(UpdateOptions.OSSProvider, OssProvider.AWS));
        [Fact] public void OSSBucketRegion() => Assert.NotNull(B().Option(UpdateOptions.OSSBucketRegion, "cn-shanghai"));
        #endregion

        #region Blacklist/Misc (5)
        [Fact] public void BlackList_Empty() => Assert.NotNull(B().Option(UpdateOptions.BlackList, BlackListConfig.Empty));
        [Fact] public void BlackList_Configured() => Assert.NotNull(B().Option(UpdateOptions.BlackList,
            new BlackListConfig(new List<string> { "*.pdb" }, new List<string> { ".log" }, new List<string> { "logs" })));
        [Fact] public void Bowl_Custom() => Assert.NotNull(B().Option(UpdateOptions.Bowl, "Bowl.exe"));
        [Fact] public void Script_Custom() => Assert.NotNull(B().Option(UpdateOptions.Script, "chmod +x /app/Update"));
        [Fact] public void Hub_Configured() => Assert.NotNull(B().Option(UpdateOptions.Hub,
            new HubConfig { Url = "https://signalr.example.com/hub" }));
        #endregion

        #region Full Combination Chains (6)
        [Fact] public void Chain_All33Options()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Client)
                .Option(UpdateOptions.DiffMode, DiffMode.Parallel)
                .Option(UpdateOptions.Encoding, Encoding.UTF8)
                .Option(UpdateOptions.Format, "ZIP")
                .Option(UpdateOptions.DownloadTimeout, 120)
                .Option(UpdateOptions.DriveEnabled, false)
                .Option(UpdateOptions.PatchEnabled, true)
                .Option(UpdateOptions.BackupEnabled, true)
                .Option(UpdateOptions.Mode, UpdateMode.Default)
                .Option(UpdateOptions.Silent, false)
                .Option(UpdateOptions.UpdateUrl, "https://update.example.com/api/v2")
                .Option(UpdateOptions.AppSecretKey, "secret-key-2026")
                .Option(UpdateOptions.AppName, "Update.exe")
                .Option(UpdateOptions.MainAppName, "MyProduct.exe")
                .Option(UpdateOptions.InstallPath, _testDir)
                .Option(UpdateOptions.ClientVersion, "1.0.0")
                .Option(UpdateOptions.UpgradeClientVersion, "0.5.0")
                .Option(UpdateOptions.MaxConcurrency, 4)
                .Option(UpdateOptions.EnableResume, true)
                .Option(UpdateOptions.RetryCount, 5)
                .Option(UpdateOptions.VerifyChecksum, true)
                .Option(UpdateOptions.RetryInterval, TimeSpan.FromSeconds(2))
                .Option(UpdateOptions.SilentAutoInstall, false)
                .Option(UpdateOptions.SilentPollIntervalMinutes, 30)
                .Option(UpdateOptions.ReportUrl, "https://telemetry.example.com/report")
                .Option(UpdateOptions.ProductId, "my-product-001")
                .Option(UpdateOptions.Scheme, "Bearer")
                .Option(UpdateOptions.Token, "jwt-token-xyz")
                .Option(UpdateOptions.PermissionScript, "#!/bin/bash\nchmod +x")
                .Option(UpdateOptions.BlackList, BlackListConfig.Empty)
                .Option(UpdateOptions.Bowl, "Bowl.exe")
                .Option(UpdateOptions.UpdateLogUrl, "https://example.com/changelog")
                .Option(UpdateOptions.Script, "chmod +x /opt/app/Update")
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

        [Fact] public void Chain_FullSecurity()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.Scheme, "HMAC").Option(UpdateOptions.Token, "hmac-secret")
                .Option(UpdateOptions.ReportUrl, "https://telemetry.example.com/report")
                .Option(UpdateOptions.VerifyChecksum, true)
                .Option(UpdateOptions.PermissionScript, "#!/bin/bash\nchmod +x /opt/app/Update")
                .SetConfig(new Configinfo { UpdateUrl = "https://secure.example.com/api", MainAppName = "SecureApp.exe", ClientVersion = "2.0.0", InstallPath = _testDir, AppSecretKey = "secure-key", Scheme = "HMAC", Token = "hmac-secret", ReportUrl = "https://telemetry.example.com/report" });
            Assert.NotNull(b);
        }

        [Fact] public void Chain_UpgradeWithExtensions()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Upgrade)
                .Option(UpdateOptions.ReportUrl, "https://telemetry.example.com/report")
                .Option(UpdateOptions.Scheme, "Bearer").Option(UpdateOptions.Token, "jwt")
                .SetConfig(new Configinfo { UpdateUrl = "https://api.example.com", MainAppName = "MyApp.exe", ClientVersion = "1.0.0", InstallPath = _testDir, AppSecretKey = "key", Scheme = "Bearer", Token = "jwt" })
                .AddListenerException((s, e) => { }).AddListenerUpdateInfo((s, e) => { })
                .AddCustomOption(new List<Func<bool>> { () => true });
            Assert.NotNull(b);
        }
        #endregion
    }
}
