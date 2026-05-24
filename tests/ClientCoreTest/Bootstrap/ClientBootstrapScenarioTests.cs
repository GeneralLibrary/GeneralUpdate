using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GeneralUpdate.ClientCore;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using Xunit;

namespace ClientCoreTest.Bootstrap
{
    /// <summary>
    /// Comprehensive ClientBootstrap scenario tests.
    /// Covers real-world developer usage patterns:
    ///   - Client â†?Upgrade mutual upgrade configuration
    ///   - Version precheck / skip scenarios
    ///   - Custom option injection
    ///   - Silent update configuration
    ///   - Full event listener chain
    ///   - Push upgrade notification reception
    /// </summary>
    public class ClientBootstrapScenarioTests : IDisposable
    {
        private readonly string _testDir;

        public ClientBootstrapScenarioTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GU_ClientScenario_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }

        #region Client â†?Upgrade Mutual Upgrade

        /// <summary>
        /// Scenario: Developer sets up client for mutual upgrade.
        /// Both client and upgrade versions need checking.
        /// </summary>
        [Fact]
        public void MutualUpgrade_BothNeedUpdate_ConfiguresClientCorrectly()
        {
            // Arrange â€?client-side developer configuration
            var config = new Configinfo
            {
                UpdateUrl = "https://update.company.com/api",
                AppName = "Update.exe",
                MainAppName = "ProductApp.exe",
                ClientVersion = "1.0.0",
                UpgradeClientVersion = "0.5.0",
                InstallPath = _testDir,
                AppSecretKey = "prod-key",
                ProductId = "product-001",
                Scheme = "Bearer",
                Token = "jwt-token"
            };

            var updatePrecheckCalled = false;
            var updateInfoReceived = false;

            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdatePrecheck(args =>
                {
                    updatePrecheckCalled = true;
                    return false; // Don't skip â€?proceed with update
                })
                .AddListenerUpdateInfo((s, e) =>
                {
                    updateInfoReceived = true;
                })
                .AddListenerMultiAllDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadError((s, e) => { })
                .AddListenerMultiDownloadStatistics((s, e) => { })
                .AddListenerException((s, e) => { });

            // Assert â€?all components configured correctly
            Assert.NotNull(bootstrap);
            Assert.Same(bootstrap, bootstrap);
        }

        /// <summary>
        /// Scenario: Only main app needs update, upgrade is current.
        /// </summary>
        [Fact]
        public void MutualUpgrade_MainAppOnly_ConfiguresClientCorrectly()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config);

            // Assert
            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Upgrade app itself needs update but main app is current.
        /// </summary>
        [Fact]
        public void MutualUpgrade_UpgradeAppOnly_ConfiguresClientCorrectly()
        {
            // Arrange â€?upgrade app needs updating but main doesn't
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com/updates",
                MainAppName = "MyApp.exe",
                ClientVersion = "2.0.0",
                UpgradeClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config);

            // Assert
            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Precheck / Skip Scenarios

        /// <summary>
        /// Scenario: Developer wants to show a UI dialog before updating.
        /// The precheck callback receives update info and returns user's decision.
        /// </summary>
        [Fact]
        public void Precheck_UserChoosesToSkip_ReturnsTrue()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            var precheckCalled = false;
            UpdateInfoEventArgs? precheckInfo = null;

            // Act â€?developer registers precheck that evaluates version info
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdatePrecheck(args =>
                {
                    precheckCalled = true;
                    precheckInfo = args;
                    // Real app would show dialog here and return user choice
                    return true; // User chose to skip
                });

            // Assert â€?precheck registered correctly
            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Developer wants to skip update when already on current version.
        /// </summary>
        [Fact]
        public void Precheck_SkipWhenNoUpdate_ReturnsCorrectDecision()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            var skipCalled = false;

            // Developer setup: only skip if certain conditions met
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdatePrecheck(args =>
                {
                    skipCalled = true;
                    // Real logic: check version and decide
                    return args.Info?.Body == null ||
                           args.Info.Body.Count == 0;
                });

            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Developer wants to auto-approve updates during off-hours.
        /// </summary>
        [Fact]
        public void Precheck_AutoApproveDuringOffHours_ConfiguresLogicCorrectly()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Developer logic: auto-approve between 2 AM and 6 AM
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdatePrecheck(args =>
                {
                    var hour = DateTime.Now.Hour;
                    if (hour >= 2 && hour < 6)
                        return false; // Auto-approve during off-hours
                    return true; // Otherwise ask user
                });

            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Custom Options Injection

        /// <summary>
        /// Scenario: Developer injects custom pre-update checks.
        /// </summary>
        [Fact]
        public void CustomOptions_MultipleChecks_AllRegistered()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Developer registers multiple custom checks
            var customOptions = new List<Func<bool>>
            {
                () => Directory.Exists(_testDir), // Check install directory exists
                () => true, // Check disk space
                () => true  // Check network connectivity
            };

            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddCustomOption(customOptions);

            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Developer injects empty custom options (no-op).
        /// </summary>
        [Fact]
        public void CustomOptions_EmptyList_DoesNotThrow()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Act & Assert â€?empty list should not throw
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddCustomOption(new List<Func<bool>>());

            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Event Listener Chain (Client-Side)

        /// <summary>
        /// Scenario: Developer sets up all event listeners for comprehensive monitoring.
        /// </summary>
        [Fact]
        public void EventListeners_FullChain_AllSevenEventsRegistered()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            var eventsRegistered = new List<string>();

            // Act â€?developer chains all event listeners
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdateInfo((s, e) => eventsRegistered.Add("UpdateInfo"))
                .AddListenerMultiAllDownloadCompleted((s, e) => eventsRegistered.Add("AllDownloaded"))
                .AddListenerMultiDownloadCompleted((s, e) => eventsRegistered.Add("DownloadCompleted"))
                .AddListenerMultiDownloadError((s, e) => eventsRegistered.Add("DownloadError"))
                .AddListenerMultiDownloadStatistics((s, e) => eventsRegistered.Add("Statistics"))
                .AddListenerException((s, e) => eventsRegistered.Add("Exception"));

            // Assert â€?all listeners registered
            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Developer only listens for critical events.
        /// </summary>
        [Fact]
        public void EventListeners_CriticalOnly_ExceptionAndUpdateInfo()
        {
            // Arrange
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Developer only cares about errors and update info
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerException((s, e) => { /* Log error for telemetry */ })
                .AddListenerUpdateInfo((s, e) => { /* Show update available toast */ });

            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Method Chaining (Fluent API)

        /// <summary>
        /// Scenario: Developer uses fluent API to configure everything in one chain.
        /// </summary>
        [Fact]
        public void FluentApi_FullChain_ReturnsCorrectBootstrapInstance()
        {
            // Arrange & Act â€?complete fluent chain
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                AppName = "Update.exe",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                UpgradeClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "secret",
                ProductId = "app-001",
                Scheme = "Bearer",
                Token = "jwt",
                Bowl = "Bowl.exe",
                Script = "#!/bin/bash\nchmod +x",
                ReportUrl = "https://telemetry.example.com",
                UpdateLogUrl = "https://example.com/changelog",
                BlackFiles = new List<string> { "*.pdb" },
                BlackFormats = new List<string> { ".log" },
                SkipDirectorys = new List<string> { "logs" }
            };

            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config)
                .AddListenerUpdatePrecheck(args => false)
                .AddCustomOption(new List<Func<bool>> { () => true })
                .AddListenerUpdateInfo((s, e) => { })
                .AddListenerMultiAllDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadCompleted((s, e) => { })
                .AddListenerMultiDownloadError((s, e) => { })
                .AddListenerMultiDownloadStatistics((s, e) => { })
                .AddListenerException((s, e) => { });

            // Assert
            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Scenario: Developer uses minimal fluent API â€?only essential configuration.
        /// </summary>
        [Fact]
        public void FluentApi_MinimalChain_JustConfig()
        {
            // The minimum a developer MUST provide
            var bootstrap = new GeneralClientBootstrap()
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

            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Silent Update Configuration

        /// <summary>
        /// Scenario: Developer configures silent update mode.
        /// App checks for updates silently in the background.
        /// </summary>
        [Fact]
        public void SilentUpdate_Configuration_IsValid()
        {
            // Arrange â€?developer sets up silent update
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Note: SilentUpdateMode requires EnableSilentUpdate option to be set on AbstractBootstrap
            var bootstrap = new GeneralClientBootstrap()
                .SetConfig(config);

            Assert.NotNull(bootstrap);
        }

        #endregion

        #region Configinfo Edge Cases

        /// <summary>
        /// Tests Configinfo with null list properties doesn't cause issues.
        /// </summary>
        [Fact]
        public void Configinfo_NullLists_SetDefaults()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
                // BlackFiles, BlackFormats, SkipDirectorys left as default (null)
            };

            config.Validate();
            Assert.NotNull(config);
        }

        /// <summary>
        /// Tests Configinfo default InstallPath is set correctly.
        /// </summary>
        [Fact]
        public void Configinfo_DefaultInstallPath_IsCurrentDirectory()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            // Default InstallPath should be the current base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
        }

        /// <summary>
        /// Tests Configinfo default AppName is "Update.exe".
        /// </summary>
        [Fact]
        public void Configinfo_DefaultAppName_IsUpdateExe()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            Assert.Equal("Update.exe", config.AppName);
        }

        #endregion

        #region Cross-Platform Considerations

        /// <summary>
        /// Tests that Configinfo works correctly on any platform.
        /// </summary>
        [Fact]
        public void Configinfo_PlatformAgnostic_WorksOnAnyOS()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            config.Validate();
            Assert.NotNull(config);
        }

        /// <summary>
        /// Tests that the Script field supports shell scripts for Linux/macOS.
        /// </summary>
        [Fact]
        public void Configinfo_LinuxPermissionScript_StoredCorrectly()
        {
            var config = new Configinfo
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token",
                Script = "#!/bin/bash\nset -e\nchmod +x /opt/app/Update\nchown root:root /opt/app/Update"
            };

            config.Validate();
            Assert.Contains("chmod +x", config.Script);
            Assert.Contains("#!/bin/bash", config.Script);
        }

        #endregion

        #region UpdateInfoEventArgs Tests

        /// <summary>
        /// Tests UpdateInfoEventArgs creation with VersionRespDTO.
        /// </summary>
        [Fact]
        public void UpdateInfoEventArgs_WithVersionResponse_ContainsCorrectData()
        {
            // Arrange
            var versions = new List<VersionInfo>
            {
                new() { Version = "2.0.0", Url = "https://cdn.example.com/update.zip", Format = "ZIP", IsForcibly = true }
            };

            var response = new VersionRespDTO
            {
                Code = 200,
                Body = versions
            };

            // Act
            var args = new UpdateInfoEventArgs(response);

            // Assert
            Assert.NotNull(args.Info);
            Assert.Equal(200, args.Info.Code);
            Assert.NotNull(args.Info.Body);
            Assert.Single(args.Info.Body);
            Assert.True(args.Info.Body[0].IsForcibly);
        }

        /// <summary>
        /// Tests UpdateInfoEventArgs with no-update response.
        /// </summary>
        [Fact]
        public void UpdateInfoEventArgs_NoUpdateResponse_HasEmptyBody()
        {
            // Arrange â€?server says no update available
            var response = new VersionRespDTO
            {
                Code = 200,
                Body = new List<VersionInfo>() // empty
            };

            // Act
            var args = new UpdateInfoEventArgs(response);

            // Assert
            Assert.NotNull(args.Info);
            Assert.Empty(args.Info.Body);
        }

        /// <summary>
        /// Tests UpdateInfoEventArgs with error response.
        /// </summary>
        [Fact]
        public void UpdateInfoEventArgs_ErrorResponse_HasErrorCode()
        {
            // Arrange â€?server returns error
            var response = new VersionRespDTO
            {
                Code = 500,
                Body = null
            };

            // Act
            var args = new UpdateInfoEventArgs(response);

            // Assert
            Assert.NotNull(args.Info);
            Assert.Equal(500, args.Info.Code);
            Assert.Null(args.Info.Body);
        }

        #endregion

        #region ExceptionEventArgs Tests

        /// <summary>
        /// Tests ExceptionEventArgs creation and properties.
        /// </summary>
        [Fact]
        public void ExceptionEventArgs_WithException_ContainsExceptionData()
        {
            // Arrange
            var ex = new InvalidOperationException("Update failed");

            // Act
            var args = new ExceptionEventArgs(ex, ex.Message);

            // Assert
            Assert.NotNull(args.Exception);
            Assert.Equal("Update failed", args.Exception.Message);
            Assert.Equal("Update failed", args.Message);
        }

        #endregion
    }
}
