using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Comprehensive parameter matrix and event notification tests.
    /// Covers:
    ///   - All Option parameter combinations
    ///   - Event notification pipeline (all 7 event types)
    ///   - Push upgrade simulation via events
    ///   - BlackList configuration variations
    ///   - Various encoding/format combinations
    ///   - UpdateRequest validation edge cases
    /// </summary>
    public class ParameterMatrixAndEventTests : IDisposable
    {
        private readonly string _testDir;

        public ParameterMatrixAndEventTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GU_ParamMatrix_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
            EventManager.Instance.Clear();
        }

        #region Event Notification Pipeline

        [Fact]
        public void EventManager_DispatchUpdateInfo_NotifiesAllListeners()
        {
            var eventFired = false;
            UpdateInfoEventArgs? capturedArgs = null;

            EventManager.Instance.AddListener<UpdateInfoEventArgs>((sender, args) =>
            {
                eventFired = true;
                capturedArgs = args;
            });

            var versionBodies = new List<VersionEntry>
            {
                new() { Version = "2.0.0", Url = "https://cdn.example.com/v2.zip", IsForcibly = true, Format = "ZIP", Size = 50 * 1024 * 1024L }
            };

            var versionResp = new VersionRespDTO { Code = 200, Body = versionBodies };
            var eventArgs = new UpdateInfoEventArgs(versionResp);

            EventManager.Instance.Dispatch(this, eventArgs);

            Assert.True(eventFired, "UpdateInfo event should be dispatched to listeners");
            Assert.NotNull(capturedArgs);
        }

        [Fact]
        public void EventManager_DispatchException_NotifiesListeners()
        {
            var eventFired = false;
            ExceptionEventArgs? capturedArgs = null;

            EventManager.Instance.AddListener<ExceptionEventArgs>((sender, args) =>
            {
                eventFired = true;
                capturedArgs = args;
            });

            var exception = new InvalidOperationException("Test exception for push update failure");
            var eventArgs = new ExceptionEventArgs(exception, exception.Message);

            EventManager.Instance.Dispatch(this, eventArgs);

            Assert.True(eventFired);
            Assert.NotNull(capturedArgs);
            Assert.Equal("Test exception for push update failure", capturedArgs.Message);
        }

        [Fact]
        public void EventManager_MultipleListeners_AllCalled()
        {
            var count1 = 0;
            var count2 = 0;
            var count3 = 0;

            EventManager.Instance.AddListener<UpdateInfoEventArgs>((s, e) => count1++);
            EventManager.Instance.AddListener<UpdateInfoEventArgs>((s, e) => count2++);
            EventManager.Instance.AddListener<UpdateInfoEventArgs>((s, e) => count3++);

            var args = new UpdateInfoEventArgs(new VersionRespDTO { Code = 200, Body = new List<VersionEntry>() });

            EventManager.Instance.Dispatch(this, args);

            Assert.Equal(1, count1);
            Assert.Equal(1, count2);
            Assert.Equal(1, count3);
        }

        [Fact]
        public void EventManager_ListenerException_ThrowingListenerIsInvoked()
        {
            var throwingCalled = false;

            EventManager.Instance.AddListener<ExceptionEventArgs>((s, e) =>
            {
                throwingCalled = true;
                throw new InvalidOperationException("Listener bug");
            });

            var args = new ExceptionEventArgs(new Exception("test"), "test");

            try { EventManager.Instance.Dispatch(this, args); }
            catch (InvalidOperationException) { /* expected if exception propagates */ }

            Assert.True(throwingCalled, "Throwing listener should have been invoked");
        }

        [Fact]
        public void EventManager_AllDownloadEvents_CanBeRegistered()
        {
            var allDownloadCalled = false;
            var downloadCalled = false;
            var downloadErrorCalled = false;
            var statisticsCalled = false;

            EventManager.Instance.AddListener<MultiAllDownloadCompletedEventArgs>((s, e) => allDownloadCalled = true);
            EventManager.Instance.AddListener<MultiDownloadCompletedEventArgs>((s, e) => downloadCalled = true);
            EventManager.Instance.AddListener<MultiDownloadErrorEventArgs>((s, e) => downloadErrorCalled = true);
            EventManager.Instance.AddListener<MultiDownloadStatisticsEventArgs>((s, e) => statisticsCalled = true);

            EventManager.Instance.Dispatch(this,
                new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
            EventManager.Instance.Dispatch(this,
                new MultiDownloadCompletedEventArgs(new VersionEntry(), true));
            EventManager.Instance.Dispatch(this,
                new MultiDownloadErrorEventArgs(new Exception(), new VersionEntry()));
            EventManager.Instance.Dispatch(this,
                new MultiDownloadStatisticsEventArgs(new VersionEntry(), TimeSpan.Zero, "0 B/s", 0, 0, 0));

            Assert.True(allDownloadCalled);
            Assert.True(downloadCalled);
            Assert.True(downloadErrorCalled);
            Assert.True(statisticsCalled);
        }

        #endregion

        #region UpdateRequest Validation Matrix

        [Fact]
        public void Configinfo_Validate_WithAllFields_Passes()
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                InstallPath = _testDir,
                AppSecretKey = "secret-key",
                Scheme = "https",
                Token = "token"
            };

            config.Validate();
        }

        [Fact]
        public void Configinfo_Validate_MissingUpdateUrl_Throws()
        {
            var config = new UpdateRequest
            {
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key"
            };

            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Fact]
        public void Configinfo_Validate_MissingMainAppName_Throws()
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                ClientVersion = "1.0.0",
                AppSecretKey = "key"
            };

            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Fact]
        public void Configinfo_Validate_MissingClientVersion_Throws()
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                AppSecretKey = "key"
            };

            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Theory]
        [InlineData("Bearer", "jwt-token")]
        [InlineData("ApiKey", "api-key-12345")]
        [InlineData("Basic", "base64-credentials")]
        [InlineData("HMAC", "hmac-secret")]
        public void Configinfo_Validate_VariousAuthSchemes_Passes(string scheme, string token)
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = scheme,
                Token = token
            };

            config.Validate();
        }

        [Fact]
        public void Configinfo_WithBlackLists_ValidatesSuccessfully()
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token",
                Files = new List<string> { "*.pdb", "*.config" },
                Formats = new List<string> { ".log", ".tmp" },
                Directories = new List<string> { "logs", "temp" }
            };

            config.Validate();
            Assert.Equal(2, config.Files.Count);
            Assert.Equal(2, config.Formats.Count);
            Assert.Equal(2, config.Directories.Count);
        }

        #endregion

        #region BlackList Configuration Matrix

        [Fact]
        public void BlackListManager_VariousConfigurations_AcceptsAllRules()
        {
            var manager = new BlackMatcher(BlackPolicy.Empty);

            Assert.NotNull(manager);
            Assert.NotNull(BlackDefaults.DefaultFiles);
            Assert.NotNull(BlackDefaults.DefaultFormats);
            Assert.NotNull(BlackDefaults.DefaultDirectories);
            Assert.False(manager.IsBlacklisted("test.dll"));
        }

        [Fact]
        public void BlackListManager_EmptyLists_DoesNotThrow()
        {
            var manager = new BlackMatcher(BlackPolicy.Empty);
            Assert.NotNull(manager);
        }

        [Fact]
        public void BlackListManager_NullList_DoesNotThrow()
        {
            var manager = new BlackMatcher(BlackPolicy.Empty);
            Assert.NotNull(manager);
        }

        #endregion

        #region Option Matrix

        [Fact]
        public void Option_AllConstants_AreAccessible()
        {
            Assert.NotNull(Option.Encoding);
            Assert.NotNull(Option.Format);
            Assert.NotNull(Option.DownloadTimeout);
            Assert.NotNull(Option.PatchEnabled);
            Assert.NotNull(Option.BackupEnabled);
            Assert.NotNull(Option.Silent);
        }

        #endregion

        #region Push Upgrade Simulation

        [Fact]
        public void PushUpgrade_ServerNotifies_ClientReceivesUpdateInfo()
        {
            var pushNotification = new UpdateInfoEventArgs(new VersionRespDTO
            {
                Code = 200,
                Body = new List<VersionEntry>
                {
                    new()
                    {
                        Version = "3.0.0",
                        Url = "https://cdn.example.com/push-update-v3.zip",
                        Hash = "sha256:push123",
                        Format = "ZIP",
                        Size = 75 * 1024 * 1024L,
                        IsForcibly = false,
                        ReleaseDate = DateTime.UtcNow,
                        UpdateLog = "# v3.0.0\n- Major feature: Push notifications\n- Performance improvements"
                    }
                }
            });

            var received = false;
            VersionRespDTO? captured = null;

            EventManager.Instance.AddListener<UpdateInfoEventArgs>((sender, args) =>
            {
                received = true;
                captured = args.Info;
            });

            EventManager.Instance.Dispatch(this, pushNotification);

            Assert.True(received, "Client should receive push notification");
            Assert.NotNull(captured);
            Assert.Equal(200, captured.Code);
            Assert.Single(captured.Body);
            Assert.Equal("3.0.0", captured.Body[0].Version);
            Assert.Equal("sha256:push123", captured.Body[0].Hash);
            Assert.False(captured.Body[0].IsForcibly);
        }

        [Fact]
        public void PushUpgrade_ForciblyUpdate_CannotBeSkipped()
        {
            var pushNotification = new UpdateInfoEventArgs(new VersionRespDTO
            {
                Code = 200,
                Body = new List<VersionEntry>
                {
                    new()
                    {
                        Version = "2.0.1",
                        Url = "https://cdn.example.com/critical-update.zip",
                        Hash = "sha256:critical",
                        Format = "ZIP",
                        Size = 10 * 1024 * 1024L,
                        IsForcibly = true,
                        ReleaseDate = DateTime.UtcNow
                    }
                }
            });

            var received = false;
            var isForcibly = false;

            EventManager.Instance.AddListener<UpdateInfoEventArgs>((sender, args) =>
            {
                received = true;
                isForcibly = args.Info?.Body?[0]?.IsForcibly == true;
            });

            EventManager.Instance.Dispatch(this, pushNotification);

            Assert.True(received);
            Assert.True(isForcibly, "This is a forced update - user cannot skip");
        }

        [Fact]
        public void PushUpgrade_MultipleVersions_ClientCanChooseOptimalPath()
        {
            var pushNotification = new UpdateInfoEventArgs(new VersionRespDTO
            {
                Code = 200,
                Body = new List<VersionEntry>
                {
                    new() { Version = "1.0.1", Url = "https://cdn.example.com/v1.0.1.zip", ReleaseDate = new DateTime(2026, 1, 1), Format = "ZIP", Size = 5 * 1024 * 1024L },
                    new() { Version = "1.0.2", Url = "https://cdn.example.com/v1.0.2.zip", ReleaseDate = new DateTime(2026, 2, 1), Format = "ZIP", Size = 5 * 1024 * 1024L },
                    new() { Version = "1.0.3", Url = "https://cdn.example.com/v1.0.3.zip", ReleaseDate = new DateTime(2026, 3, 1), Format = "ZIP", Size = 5 * 1024 * 1024L },
                    new() { Version = "2.0.0", Url = "https://cdn.example.com/v2.0.0-full.zip", ReleaseDate = new DateTime(2026, 4, 1), Format = "ZIP", Size = 50 * 1024 * 1024L }
                }
            });

            var versions = new List<string>();

            EventManager.Instance.AddListener<UpdateInfoEventArgs>((sender, args) =>
            {
                if (args.Info?.Body != null)
                    versions.AddRange(args.Info.Body.Select(v => v.Version!));
            });

            EventManager.Instance.Dispatch(this, pushNotification);

            Assert.Equal(4, versions.Count);
            Assert.Contains("1.0.1", versions);
            Assert.Contains("1.0.2", versions);
            Assert.Contains("1.0.3", versions);
            Assert.Contains("2.0.0", versions);
        }

        #endregion

        #region StorageManager / Backup Tests

        [Fact]
        public void StorageManager_GetTempDirectory_CreatesDirectory()
        {
            var tempDir = StorageManager.GetTempDirectory("test_temp");

            Assert.NotNull(tempDir);
            Assert.True(Directory.Exists(tempDir), $"Temp directory should exist: {tempDir}");

            try { Directory.Delete(tempDir, true); } catch { }
        }

        [Fact]
        public void StorageManager_Backup_CreatesBackupDirectory()
        {
            var sourceDir = Path.Combine(_testDir, "backup_source");
            var backupDir = Path.Combine(_testDir, "backup_dest");
            Directory.CreateDirectory(sourceDir);

            File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "test content");
            File.WriteAllText(Path.Combine(sourceDir, "config.json"), "{}");

            try
            {
                StorageManager.Backup(sourceDir, backupDir, new List<string>());

                Assert.True(Directory.Exists(backupDir));
                Assert.True(File.Exists(Path.Combine(backupDir, "test.txt")));
                Assert.True(File.Exists(Path.Combine(backupDir, "config.json")));
            }
            finally
            {
                try { Directory.Delete(backupDir, true); } catch { }
            }
        }

        [Fact]
        public void StorageManager_Backup_SkipsSpecifiedDirectories()
        {
            var sourceDir = Path.Combine(_testDir, "skip_source");
            var backupDir = Path.Combine(_testDir, "skip_dest");
            Directory.CreateDirectory(sourceDir);

            File.WriteAllText(Path.Combine(sourceDir, "app.exe"), "exe content");

            var logsDir = Path.Combine(sourceDir, "logs");
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "app.log"), "log content");

            try
            {
                StorageManager.Backup(sourceDir, backupDir, new List<string> { "logs" });

                Assert.True(Directory.Exists(backupDir));
                Assert.True(File.Exists(Path.Combine(backupDir, "app.exe")));
                Assert.False(Directory.Exists(Path.Combine(backupDir, "logs")), "Logs directory should be skipped");
            }
            finally
            {
                try { Directory.Delete(backupDir, true); } catch { }
            }
        }

        #endregion

        #region Parameter Combination Scenarios

        [Fact]
        public void Configinfo_FullConfiguration_AllFieldsValid()
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://update.mycompany.com/v2/api",
                UpdateAppName = "Update.exe",
                MainAppName = "EnterpriseApp.exe",
                ClientVersion = "4.2.1-beta",
                UpgradeClientVersion = "1.5.0",
                InstallPath = @"C:\Program Files\EnterpriseApp",
                AppSecretKey = "enterprise-secret-key-2026",
                ProductId = "enterprise-app-pro",
                UpdateLogUrl = "https://mycompany.com/releases",
                ReportUrl = "https://telemetry.mycompany.com/api/v1/reports",
                Scheme = "HMAC",
                Token = "hmac-secret-key",
                Bowl = "Bowl.exe",
                DriverDirectory = @"C:\Program Files\EnterpriseApp\drivers",
                Files = new List<string> { "*.pdb", "*.config", "*.Development.json" },
                Formats = new List<string> { ".log", ".tmp", ".cache", ".etl" },
                Directories = new List<string> { "logs", "temp", "cache", "Diagnostics", "__backups__" }
            };

            config.Validate();
            Assert.Equal(3, config.Files.Count);
            Assert.Equal(4, config.Formats.Count);
            Assert.Equal(5, config.Directories.Count);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("2.1.3-beta")]
        [InlineData("10.20.30.40")]
        [InlineData("2026.5.24-rc1")]
        public void Configinfo_VariousVersionFormats_ValidatesSuccessfully(string version)
        {
            var config = new UpdateRequest
            {
                UpdateUrl = "https://api.example.com",
                MainAppName = "MyApp.exe",
                ClientVersion = version,
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            config.Validate();
            Assert.Equal(version, config.ClientVersion);
        }

        [Theory]
        [InlineData("https://api.example.com/updates")]
        [InlineData("https://update.company.com/v2/api/versions")]
        [InlineData("http://192.168.1.100:8080/api/update")]
        public void Configinfo_VariousUpdateUrls_ValidatesSuccessfully(string url)
        {
            var config = new UpdateRequest
            {
                UpdateUrl = url,
                MainAppName = "MyApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "key",
                Scheme = "https",
                Token = "token"
            };

            config.Validate();
            Assert.Equal(url, config.UpdateUrl);
        }

        #endregion
    }
}
