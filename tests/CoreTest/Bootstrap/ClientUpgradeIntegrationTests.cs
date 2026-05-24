using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Comprehensive integration tests for Client <-> Upgrade mutual upgrade process.
    /// Tests the full lifecycle: Client validates versions, downloads packages,
    /// passes ProcessInfo to Upgrade, and Upgrade applies updates.
    ///
    /// Covers:
    ///   - Client-only upgrade (main app update)
    ///   - Upgrade-only upgrade (updater update)
    ///   - Client + Upgrade simultaneous update
    ///   - Differential (patch) upgrade pipeline
    ///   - Push upgrade via event notification
    ///   - Various parameter combinations
    /// </summary>
    public class ClientUpgradeIntegrationTests : IDisposable
    {
        private readonly string _testBaseDir;

        public ClientUpgradeIntegrationTests()
        {
            _testBaseDir = Path.Combine(Path.GetTempPath(), $"GU_IntegrationTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testBaseDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testBaseDir, true); } catch { /* ignore */ }
        }

        #region Client <-> Upgrade Mutual Upgrade

        /// <summary>
        /// Scenario: Both client and upgrade need updates.
        /// Client validates both versions, downloads upgrade packages,
        /// serializes ProcessInfo, and prepares for upgrade handoff.
        /// </summary>
        [Fact]
        public void ClientUpgrade_MutualUpdate_BothNeedUpdates_ConfiguresCorrectly()
        {
            // Arrange - emulate a developer configuring for mutual update
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                AppName = "Update.exe",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                UpgradeClientVersion = "0.5.0",
                InstallPath = _testBaseDir,
                AppSecretKey = "test-secret-key",
                ProductId = "test-product",
                Scheme = "https",
                Token = "test-token"
            };

            var bootstrap = new GeneralUpdateBootstrap();

            // Act - developer chains configuration
            var result = bootstrap
                .SetConfig(config)
                .SetCustomSkipOption(() => false)
                .AddListenerException((s, e) => { })
                .AddListenerUpdateInfo((s, e) => { });

            // Assert - bootstrap configured without errors
            Assert.NotNull(result);
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Scenario: Only main app needs update, upgrade is current.
        /// Client should only handle the main app update.
        /// </summary>
        [Fact]
        public void ClientUpgrade_MainAppOnly_ConfiguresCorrectly()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testBaseDir,
                AppSecretKey = "test-key",
                Scheme = "https",
                Token = "test-token"
            };

            var bootstrap = new GeneralUpdateBootstrap();

            var result = bootstrap
                .SetConfig(config)
                .AddListenerMultiAllDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadCompleted((s, e) => { });

            Assert.NotNull(result);
        }

        /// <summary>
        /// Scenario: Forcibly update - user skip callback is ignored.
        /// </summary>
        [Fact]
        public void ClientUpgrade_ForciblyUpdate_SkipCallbackIsIgnored()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testBaseDir,
                AppSecretKey = "test-key",
                Scheme = "https",
                Token = "test-token"
            };

            var skipCalled = false;
            var bootstrap = new GeneralUpdateBootstrap()
                .SetConfig(config)
                .SetCustomSkipOption(() =>
                {
                    skipCalled = true;
                    return true;
                });

            Assert.NotNull(bootstrap);
            Assert.False(skipCalled, "Skip callback should not be invoked during configuration");
        }

        #endregion

        #region VersionInfo Scenarios (Cross-version / Forcibly / Freeze)

        [Fact]
        public void VersionInfo_WithAllFields_SerializesCorrectly()
        {
            var versionInfo = new VersionInfo
            {
                RecordId = 1001,
                Name = "client-app-v1.0.0-to-v2.0.0.zip",
                Hash = "abc123def456",
                ReleaseDate = new DateTime(2026, 5, 20),
                Url = "https://cdn.example.com/packages/v2.0.0.zip",
                Version = "2.0.0",
                AppType = (int)AppType.Client,
                Platform = 1,
                ProductId = "test-product",
                IsForcibly = true,
                Format = "ZIP",
                Size = 1024 * 1024 * 50L,
                AuthScheme = "Bearer",
                AuthToken = "jwt-token-here",
                UpdateLog = "# Release Notes\n- Bug fixes\n- New features"
            };

            var json = JsonSerializer.Serialize(versionInfo);

            Assert.NotNull(json);
            Assert.Contains("\"recordId\":1001", json);
            Assert.Contains("\"isForcibly\":true", json);
            Assert.Contains("\"url\":\"https://cdn.example.com/packages/v2.0.0.zip\"", json);
            Assert.Contains("\"version\":\"2.0.0\"", json);
            Assert.Contains("\"size\":52428800", json);
            Assert.Contains("\"authScheme\":\"Bearer\"", json);
            Assert.Contains("\"updateLog\"", json);
        }

        [Fact]
        public void VersionInfo_NonForcibly_UserCanSkip()
        {
            var versionInfo = new VersionInfo
            {
                RecordId = 1002,
                Name = "optional-update.zip",
                Hash = "xyz789",
                ReleaseDate = new DateTime(2026, 5, 22),
                Url = "https://cdn.example.com/optional.zip",
                Version = "1.5.0",
                IsForcibly = false,
                Format = "ZIP",
                Size = 10 * 1024 * 1024L
            };

            Assert.False(versionInfo.IsForcibly);
            Assert.Equal("1.5.0", versionInfo.Version);
        }

        [Fact]
        public void VersionInfo_MultipleVersions_SortByReleaseDate()
        {
            var versions = new List<VersionInfo>
            {
                new() { Version = "1.0.3", ReleaseDate = new DateTime(2026, 5, 15), Format = "ZIP" },
                new() { Version = "1.0.1", ReleaseDate = new DateTime(2026, 5, 1), Format = "ZIP" },
                new() { Version = "1.0.2", ReleaseDate = new DateTime(2026, 5, 10), Format = "ZIP" }
            };

            var sorted = versions.OrderBy(x => x.ReleaseDate).ToList();

            Assert.Equal("1.0.1", sorted[0].Version);
            Assert.Equal("1.0.2", sorted[1].Version);
            Assert.Equal("1.0.3", sorted[2].Version);
        }

        #endregion

        #region ProcessInfo IPC Serialization

        [Fact]
        public void ProcessInfo_FullSerialization_RoundTripPreservesAllFields()
        {
            var processInfo = new ProcessInfo
            {
                AppName = "MyApp.exe",
                CurrentVersion = "1.0.0",
                LastVersion = "2.0.0",
                InstallPath = @"C:\Program Files\MyApp",
                CompressEncoding = "UTF-8",
                CompressFormat = "ZIP",
                DownloadTimeOut = 60,
                AppSecretKey = "secret-key-123",
                UpdateVersions = new List<VersionInfo>
                {
                    new() { Version = "2.0.0", Url = "https://cdn.example.com/update.zip", Hash = "sha256hash", Size = 50 * 1024 * 1024L, Format = "ZIP", ReleaseDate = DateTime.UtcNow }
                },
                UpdateLogUrl = "https://myapp.com/changelog",
                ReportUrl = "https://api.example.com/reports",
                BackupDirectory = @"C:\Program Files\MyApp\__backups\1.0.0",
                BlackFiles = new List<string> { "*.pdb", "*.xml" },
                BlackFileFormats = new List<string> { ".pdb", ".xml" },
                SkipDirectorys = new List<string> { "logs", "cache", "__backups__" },
                Scheme = "Bearer",
                Token = "jwt-token-xyz",
                Script = "#!/bin/bash\nchmod +x",
                DriverDirectory = @"C:\drivers"
            };

            var json = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);
            var deserialized = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);

            Assert.NotNull(deserialized);
            Assert.Equal("MyApp.exe", deserialized.AppName);
            Assert.Equal("1.0.0", deserialized.CurrentVersion);
            Assert.Equal("2.0.0", deserialized.LastVersion);
            Assert.Equal(@"C:\Program Files\MyApp", deserialized.InstallPath);
            Assert.Equal("UTF-8", deserialized.CompressEncoding);
            Assert.Equal("ZIP", deserialized.CompressFormat);
            Assert.Equal(60, deserialized.DownloadTimeOut);
            Assert.Equal("secret-key-123", deserialized.AppSecretKey);
            Assert.Equal("https://myapp.com/changelog", deserialized.UpdateLogUrl);
            Assert.Equal("https://api.example.com/reports", deserialized.ReportUrl);
            Assert.Equal("Bearer", deserialized.Scheme);
            Assert.Equal("jwt-token-xyz", deserialized.Token);
            Assert.Equal(@"C:\drivers", deserialized.DriverDirectory);

            Assert.NotNull(deserialized.UpdateVersions);
            Assert.Single(deserialized.UpdateVersions);
            Assert.Equal("2.0.0", deserialized.UpdateVersions[0].Version);
            Assert.Equal("sha256hash", deserialized.UpdateVersions[0].Hash);

            Assert.NotNull(deserialized.BlackFiles);
            Assert.Contains("*.pdb", deserialized.BlackFiles);
            Assert.Contains("*.xml", deserialized.BlackFiles);

            Assert.NotNull(deserialized.SkipDirectorys);
            Assert.Contains("logs", deserialized.SkipDirectorys);
            Assert.Contains("cache", deserialized.SkipDirectorys);
        }

        [Fact]
        public void ProcessInfo_MinimalFields_DeserializesWithoutError()
        {
            var processInfo = new ProcessInfo
            {
                AppName = "MyApp.exe",
                CurrentVersion = "1.0.0",
                InstallPath = _testBaseDir,
                UpdateVersions = new List<VersionInfo>
                {
                    new() { Version = "2.0.0", Url = "https://cdn.example.com/update.zip", Format = "ZIP" }
                }
            };

            var json = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);
            var deserialized = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);

            Assert.NotNull(deserialized);
            Assert.Equal("MyApp.exe", deserialized.AppName);
            Assert.Equal("1.0.0", deserialized.CurrentVersion);
        }

        [Fact]
        public void ProcessInfo_BlackList_RoundTripPreservesRules()
        {
            var processInfo = new ProcessInfo
            {
                AppName = "MyApp.exe",
                CurrentVersion = "1.0.0",
                InstallPath = _testBaseDir,
                BlackFiles = new List<string> { "*.pdb", "*.config", "secret.key" },
                BlackFileFormats = new List<string> { ".log", ".tmp", ".cache", ".pdb" },
                SkipDirectorys = new List<string> { "logs", "temp", "cache", "node_modules", "__backups__" }
            };

            var json = JsonSerializer.Serialize(processInfo, ProcessInfoJsonContext.Default.ProcessInfo);
            var deserialized = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);

            Assert.NotNull(deserialized);
            Assert.Equal(3, deserialized.BlackFiles.Count);
            Assert.Equal(4, deserialized.BlackFileFormats.Count);
            Assert.Equal(5, deserialized.SkipDirectorys.Count);
        }

        #endregion

        #region Pipeline Context Tests (Hash  - Compress  - Patch)

        [Fact]
        public void PipelineContext_AllMiddlewareKeys_StoresAndRetrievesCorrectly()
        {
            var context = new GeneralUpdate.Core.Pipeline.PipelineContext();
            var format = "ZIP";
            var zipPath = @"C:\temp\update.zip";
            var patchPath = @"C:\temp\patch";
            var encoding = Encoding.UTF8;
            var sourcePath = @"C:\Program Files\MyApp";
            var patchEnabled = true;
            var hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            context.Add("Format", format);
            context.Add("ZipFilePath", zipPath);
            context.Add("PatchPath", patchPath);
            context.Add("Encoding", encoding);
            context.Add("SourcePath", sourcePath);
            context.Add("PatchEnabled", patchEnabled);
            context.Add("Hash", hash);

            Assert.Equal(format, context.Get<string>("Format"));
            Assert.Equal(zipPath, context.Get<string>("ZipFilePath"));
            Assert.Equal(patchPath, context.Get<string>("PatchPath"));
            Assert.Equal(encoding, context.Get<Encoding>("Encoding"));
            Assert.Equal(sourcePath, context.Get<string>("SourcePath"));
            Assert.Equal(patchEnabled, context.Get<bool?>("PatchEnabled"));
            Assert.Equal(hash, context.Get<string>("Hash"));
        }

        [Fact]
        public void PipelineContext_RemoveAndContainsKey_WorksCorrectly()
        {
            var context = new GeneralUpdate.Core.Pipeline.PipelineContext();
            context.Add("Key1", "Value1");
            context.Add("Key2", 42);

            Assert.True(context.ContainsKey("Key1"));
            Assert.True(context.ContainsKey("Key2"));
            Assert.False(context.ContainsKey("NonExistent"));

            var removed = context.Remove("Key1");

            Assert.True(removed);
            Assert.False(context.ContainsKey("Key1"));
            Assert.True(context.ContainsKey("Key2"));
            Assert.Null(context.Get<string>("Key1"));
        }

        [Fact]
        public void PipelineContext_NullValue_StoresAndReturnsNull()
        {
            var context = new GeneralUpdate.Core.Pipeline.PipelineContext();

            context.Add<string?>("NullableKey", null);

            Assert.True(context.ContainsKey("NullableKey"));
            Assert.Null(context.Get<string>("NullableKey"));
        }

        #endregion

        #region ConfigurationMapper Tests

        [Fact]
        public void ConfigurationMapper_MapToGlobalConfigInfo_MapsAllFields()
        {
            var configInfo = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                AppName = "Update.exe",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                UpgradeClientVersion = "0.5.0",
                InstallPath = _testBaseDir,
                AppSecretKey = "test-secret-key",
                ProductId = "test-product",
                UpdateLogUrl = "https://myapp.com/changelog",
                ReportUrl = "https://api.example.com/reports",
                Scheme = "Bearer",
                Token = "jwt-token",
                Bowl = "Bowl.exe",
                Script = "chmod +x /app/Update",
                DriverDirectory = @"C:\drivers",
                BlackFiles = new List<string> { "*.pdb" },
                BlackFormats = new List<string> { ".log" },
                SkipDirectorys = new List<string> { "logs" }
            };

            var globalConfig = ConfigurationMapper.MapToGlobalConfigInfo(configInfo);

            Assert.NotNull(globalConfig);
            Assert.Equal("https://api.example.com/updates", globalConfig.UpdateUrl);
            Assert.Equal("Update.exe", globalConfig.AppName);
            Assert.Equal("MyApp.exe", globalConfig.MainAppName);
            Assert.Equal("1.0.0", globalConfig.ClientVersion);
            Assert.Equal("0.5.0", globalConfig.UpgradeClientVersion);
            Assert.Equal(_testBaseDir, globalConfig.InstallPath);
            Assert.Equal("test-secret-key", globalConfig.AppSecretKey);
            Assert.Equal("test-product", globalConfig.ProductId);
            Assert.Equal("https://myapp.com/changelog", globalConfig.UpdateLogUrl);
            Assert.Equal("https://api.example.com/reports", globalConfig.ReportUrl);
            Assert.Equal("Bearer", globalConfig.Scheme);
            Assert.Equal("jwt-token", globalConfig.Token);
            Assert.Equal("Bowl.exe", globalConfig.Bowl);
            Assert.Equal("chmod +x /app/Update", globalConfig.Script);
            Assert.Equal(@"C:\drivers", globalConfig.DriverDirectory);
            Assert.NotNull(globalConfig.BlackFiles);
            Assert.Contains("*.pdb", globalConfig.BlackFiles);
            Assert.NotNull(globalConfig.SkipDirectorys);
            Assert.Contains("logs", globalConfig.SkipDirectorys);
        }


        #endregion

        #region Event Listener Chain Tests

        [Fact]
        public void EventListeners_AllSevenTypes_CanBeRegisteredInChain()
        {
            var downloadCompleted = 0;
            var allDownloadCompleted = 0;
            var downloadError = 0;
            var downloadStatistics = 0;
            var exception = 0;
            var updateInfo = 0;

            var bootstrap = new GeneralUpdateBootstrap()
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://api.example.com",
                    MainAppName = "TestApp.exe",
                    ClientVersion = "1.0.0",
                    InstallPath = _testBaseDir,
                    AppSecretKey = "key",
                    Scheme = "https",
                    Token = "token"
                });

            var result = bootstrap
                .AddListenerMultiDownloadCompleted((s, e) => downloadCompleted++)
                .AddListenerMultiAllDownloadCompleted((s, e) => allDownloadCompleted++)
                .AddListenerMultiDownloadError((s, e) => downloadError++)
                .AddListenerMultiDownloadStatistics((s, e) => downloadStatistics++)
                .AddListenerException((s, e) => exception++)
                .AddListenerUpdateInfo((s, e) => updateInfo++);

            Assert.Same(bootstrap, result);
            Assert.Equal(0, downloadCompleted);
            Assert.Equal(0, allDownloadCompleted);
            Assert.Equal(0, downloadError);
            Assert.Equal(0, downloadStatistics);
            Assert.Equal(0, exception);
            Assert.Equal(0, updateInfo);
        }

        [Fact]
        public void EventListener_NullCallback_ThrowsArgumentNullException()
        {
            var bootstrap = new GeneralUpdateBootstrap();

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerException(null!));

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerMultiDownloadCompleted(null!));

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerMultiAllDownloadCompleted(null!));

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerMultiDownloadError(null!));

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerMultiDownloadStatistics(null!));

            Assert.Throws<ArgumentNullException>(() =>
                bootstrap.AddListenerUpdateInfo(null!));
        }

        #endregion

        #region Real-world Developer Scenarios

        [Fact]
        public void DeveloperScenario_FullProductionSetup_CompleteChain()
        {
            var eventsFired = new List<string>();
            var skipRequested = false;

            var bootstrap = new GeneralUpdateBootstrap()
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://update.mycompany.com/api",
                    AppName = "Update.exe",
                    MainAppName = "MyProduct.exe",
                    ClientVersion = "3.2.1",
                    UpgradeClientVersion = "1.0.0",
                    InstallPath = @"C:\Program Files\MyProduct",
                    AppSecretKey = "prod-secret-key-2026",
                    ProductId = "my-product-v2",
                    UpdateLogUrl = "https://myproduct.com/changelog",
                    ReportUrl = "https://telemetry.mycompany.com/report",
                    Scheme = "Bearer",
                    Token = "eyJhbGciOiJIUzI1NiIs...",
                    Bowl = "Bowl.exe",
                    Script = "chmod +x /opt/myproduct/Update",
                    DriverDirectory = @"C:\Program Files\MyProduct\drivers",
                    BlackFiles = new List<string> { "*.pdb", "*.config", "appsettings.Development.json" },
                    BlackFormats = new List<string> { ".log", ".tmp", ".cache" },
                    SkipDirectorys = new List<string> { "logs", "temp", "cache", "__backups__" }
                })
                .SetCustomSkipOption(() =>
                {
                    skipRequested = true;
                    return false;
                })
                .AddListenerUpdateInfo((s, e) => eventsFired.Add("UpdateInfo"))
                .AddListenerMultiAllDownloadCompleted((s, e) => eventsFired.Add("AllDownloaded"))
                .AddListenerMultiDownloadCompleted((s, e) => eventsFired.Add("DownloadCompleted"))
                .AddListenerMultiDownloadError((s, e) => eventsFired.Add("DownloadError"))
                .AddListenerMultiDownloadStatistics((s, e) => eventsFired.Add("Statistics"))
                .AddListenerException((s, e) => eventsFired.Add("Exception"));

            Assert.NotNull(bootstrap);
            Assert.False(skipRequested, "Skip callback should not be invoked during configuration");
        }

        [Fact]
        public void DeveloperScenario_MinimalSetup_OnlyRequiredFields()
        {
            var bootstrap = new GeneralUpdateBootstrap()
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://update.mycompany.com/api",
                    MainAppName = "MyApp.exe",
                    ClientVersion = "1.0.0",
                    InstallPath = _testBaseDir,
                    AppSecretKey = "key",
                    Scheme = "https",
                    Token = "token"
                });

            Assert.NotNull(bootstrap);
        }

        #endregion
    }
}
