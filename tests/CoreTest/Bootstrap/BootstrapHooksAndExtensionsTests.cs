using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Hooks;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Integration tests for Hooks mechanism, UpdateReporter,
    /// IUpdateEventListener, and extension point models.
    ///
    /// Covers:
    ///   - IUpdateHooks lifecycle (OnBeforeUpdate, OnDownloadCompleted, OnAfterUpdate, OnError, OnBeforeStartApp)
    ///   - Custom IUpdateHooks implementation with lifecycle tracking
    ///   - NoOpUpdateHooks default behavior
    ///   - UpdateContext and DownloadContext data models
    ///   - IUpdateReporter / UpdateReport / UpdateEvent types
    ///   - IUpdateEventListener batch listener
    ///   - Security/Scheme extensibility via UpdateOptions
    ///   - HubConfig model
    /// </summary>
    public class BootstrapHooksAndExtensionsTests : IDisposable
    {
        private readonly string _testDir;

        public BootstrapHooksAndExtensionsTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"GU_HooksExt_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
        }

        #region Custom TrackingHooks

        private sealed class TrackingHooks : IUpdateHooks
        {
            public bool BeforeUpdateCalled { get; private set; }
            public bool DownloadCompletedCalled { get; private set; }
            public bool AfterUpdateCalled { get; private set; }
            public bool ErrorCalled { get; private set; }
            public bool BeforeStartAppCalled { get; private set; }

            public UpdateContext? BeforeCtx { get; private set; }
            public DownloadContext? DownloadCtx { get; private set; }
            public Exception? CapturedError { get; private set; }

            public Task<bool> OnBeforeUpdateAsync(UpdateContext ctx)
            {
                BeforeUpdateCalled = true;
                BeforeCtx = ctx;
                return Task.FromResult(true);
            }

            public Task OnDownloadCompletedAsync(DownloadContext ctx)
            {
                DownloadCompletedCalled = true;
                DownloadCtx = ctx;
                return Task.CompletedTask;
            }

            public Task OnAfterUpdateAsync(UpdateContext ctx)
            {
                AfterUpdateCalled = true;
                return Task.CompletedTask;
            }

            public Task OnUpdateErrorAsync(UpdateContext ctx, Exception ex)
            {
                ErrorCalled = true;
                CapturedError = ex;
                return Task.CompletedTask;
            }

            public Task OnBeforeStartAppAsync(UpdateContext ctx)
            {
                BeforeStartAppCalled = true;
                return Task.CompletedTask;
            }
        }

        #endregion

        #region Hook Lifecycle

        [Fact]
        public void TrackingHooks_InitialState_AllFlagsFalse()
        {
            var hooks = new TrackingHooks();
            Assert.False(hooks.BeforeUpdateCalled);
            Assert.False(hooks.DownloadCompletedCalled);
            Assert.False(hooks.AfterUpdateCalled);
            Assert.False(hooks.ErrorCalled);
            Assert.False(hooks.BeforeStartAppCalled);
        }

        [Fact]
        public async Task TrackingHooks_OnBeforeUpdate_ReturnsTrueAndRecordsContext()
        {
            var hooks = new TrackingHooks();
            var ctx = new UpdateContext("MyApp.exe", "/install", "1.0.0", "2.0.0", AppType.Client);

            var result = await hooks.OnBeforeUpdateAsync(ctx);

            Assert.True(result);
            Assert.True(hooks.BeforeUpdateCalled);
            Assert.NotNull(hooks.BeforeCtx);
            Assert.Equal("MyApp.exe", hooks.BeforeCtx.AppName);
            Assert.Equal("1.0.0", hooks.BeforeCtx.CurrentVersion);
            Assert.Equal("2.0.0", hooks.BeforeCtx.TargetVersion);
            Assert.Equal(AppType.Client, hooks.BeforeCtx.AppType);
        }

        [Fact]
        public async Task TrackingHooks_OnBeforeUpdate_CanReject()
        {
            var rejectingHooks = new RejectingHooks();
            var ctx = new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client);

            var result = await rejectingHooks.OnBeforeUpdateAsync(ctx);

            Assert.False(result, "Hook should be able to reject update");
        }

        private sealed class RejectingHooks : IUpdateHooks
        {
            public Task<bool> OnBeforeUpdateAsync(UpdateContext ctx) => Task.FromResult(false);
            public Task OnDownloadCompletedAsync(DownloadContext ctx) => Task.CompletedTask;
            public Task OnAfterUpdateAsync(UpdateContext ctx) => Task.CompletedTask;
            public Task OnUpdateErrorAsync(UpdateContext ctx, Exception ex) => Task.CompletedTask;
            public Task OnBeforeStartAppAsync(UpdateContext ctx) => Task.CompletedTask;
        }

        [Fact]
        public async Task TrackingHooks_OnDownloadCompleted_Success()
        {
            var hooks = new TrackingHooks();
            var ctx = new DownloadContext("update.zip", "2.0.0", 50 * 1024 * 1024L, TimeSpan.FromSeconds(30), "/tmp/update.zip", true);

            await hooks.OnDownloadCompletedAsync(ctx);

            Assert.True(hooks.DownloadCompletedCalled);
            Assert.NotNull(hooks.DownloadCtx);
            Assert.Equal("update.zip", hooks.DownloadCtx.AssetName);
            Assert.Equal("2.0.0", hooks.DownloadCtx.Version);
            Assert.True(hooks.DownloadCtx.Success);
        }

        [Fact]
        public async Task TrackingHooks_OnDownloadCompleted_Failure()
        {
            var hooks = new TrackingHooks();
            var ctx = new DownloadContext("corrupt.zip", "2.0.0", 0, TimeSpan.FromSeconds(5), null, false);

            await hooks.OnDownloadCompletedAsync(ctx);

            Assert.True(hooks.DownloadCompletedCalled);
            Assert.NotNull(hooks.DownloadCtx);
            Assert.False(hooks.DownloadCtx.Success);
        }

        [Fact]
        public async Task TrackingHooks_OnAfterUpdate_RecordsCall()
        {
            var hooks = new TrackingHooks();
            var ctx = new UpdateContext("MyApp.exe", "/install", "1.0.0", "2.0.0", AppType.Client);

            await hooks.OnAfterUpdateAsync(ctx);

            Assert.True(hooks.AfterUpdateCalled);
        }

        [Fact]
        public async Task TrackingHooks_OnUpdateError_CapturesException()
        {
            var hooks = new TrackingHooks();
            var ctx = new UpdateContext("MyApp.exe", "/install", "1.0.0", "2.0.0", AppType.Client);
            var ex = new InvalidOperationException("Hash verification failed");

            await hooks.OnUpdateErrorAsync(ctx, ex);

            Assert.True(hooks.ErrorCalled);
            Assert.NotNull(hooks.CapturedError);
            Assert.Equal("Hash verification failed", hooks.CapturedError.Message);
        }

        [Fact]
        public async Task TrackingHooks_OnBeforeStartApp_RecordsCall()
        {
            var hooks = new TrackingHooks();
            var ctx = new UpdateContext("MyApp.exe", "/install", "2.0.0", null, AppType.Client);

            await hooks.OnBeforeStartAppAsync(ctx);

            Assert.True(hooks.BeforeStartAppCalled);
        }

        [Fact]
        public async Task TrackingHooks_FullLifecycle_AllFiveMethodsCalled()
        {
            var hooks = new TrackingHooks();
            var beforeCtx = new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client);
            var downloadCtx = new DownloadContext("pkg.zip", "2.0.0", 100, TimeSpan.FromSeconds(10), "/tmp/pkg.zip", true);
            var afterCtx = new UpdateContext("App.exe", "/app", "2.0.0", null, AppType.Client);

            await hooks.OnBeforeUpdateAsync(beforeCtx);
            await hooks.OnDownloadCompletedAsync(downloadCtx);
            await hooks.OnAfterUpdateAsync(afterCtx);
            await hooks.OnBeforeStartAppAsync(afterCtx);

            Assert.True(hooks.BeforeUpdateCalled);
            Assert.True(hooks.DownloadCompletedCalled);
            Assert.True(hooks.AfterUpdateCalled);
            Assert.True(hooks.BeforeStartAppCalled);
            Assert.False(hooks.ErrorCalled); // No error injected
        }

        #endregion

        #region NoOpUpdateHooks

        [Fact]
        public async Task NoOpUpdateHooks_AllMethods_ReturnDefaults()
        {
            var hooks = new NoOpUpdateHooks();
            var ctx = new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client);
            var dlCtx = new DownloadContext("pkg.zip", "2.0.0", 100, TimeSpan.FromSeconds(10), "/tmp/pkg.zip", true);

            var beforeResult = await hooks.OnBeforeUpdateAsync(ctx);
            Assert.True(beforeResult);

            await hooks.OnDownloadCompletedAsync(dlCtx);
            await hooks.OnAfterUpdateAsync(ctx);
            await hooks.OnUpdateErrorAsync(ctx, new Exception("test"));
            await hooks.OnBeforeStartAppAsync(ctx);
            // No-op hooks should never throw
        }

        #endregion

        #region UpdateContext & DownloadContext

        [Fact]
        public void UpdateContext_AllFields_SetCorrectly()
        {
            var ctx = new UpdateContext("MyApp.exe", "/opt/app", "3.2.1", "4.0.0", AppType.Client);

            Assert.Equal("MyApp.exe", ctx.AppName);
            Assert.Equal("/opt/app", ctx.InstallPath);
            Assert.Equal("3.2.1", ctx.CurrentVersion);
            Assert.Equal("4.0.0", ctx.TargetVersion);
            Assert.Equal(AppType.Client, ctx.AppType);
        }

        [Fact]
        public void UpdateContext_UpgradeType()
        {
            var ctx = new UpdateContext("Update.exe", "/opt/updater", "1.0.0", "1.5.0", AppType.Upgrade);
            Assert.Equal(AppType.Upgrade, ctx.AppType);
        }

        [Fact]
        public void UpdateContext_TargetVersionCanBeNull()
        {
            var ctx = new UpdateContext("App.exe", "/app", "2.0.0", null, AppType.Client);
            Assert.Null(ctx.TargetVersion);
        }

        [Fact]
        public void DownloadContext_Successful()
        {
            var ctx = new DownloadContext("client-v2.0.0.zip", "2.0.0",
                1024L * 1024 * 50, TimeSpan.FromMinutes(2), "/tmp/update/client-v2.0.0.zip", true);

            Assert.Equal("client-v2.0.0.zip", ctx.AssetName);
            Assert.Equal("2.0.0", ctx.Version);
            Assert.Equal(52428800, ctx.TotalBytes);
            Assert.Equal(TimeSpan.FromMinutes(2), ctx.Duration);
            Assert.Equal("/tmp/update/client-v2.0.0.zip", ctx.LocalPath);
            Assert.True(ctx.Success);
        }

        [Fact]
        public void DownloadContext_Failed()
        {
            var ctx = new DownloadContext("failed-pkg.zip", "2.0.0", 0, TimeSpan.FromSeconds(3), null, false);

            Assert.False(ctx.Success);
            Assert.Null(ctx.LocalPath);
        }

        #endregion

        #region UpdateReport / IUpdateReporter

        [Fact]
        public void UpdateReport_StartedEvent()
        {
            var report = new UpdateReport("MyApp.exe", "1.0.0", "2.0.0",
                UpdateEvent.UpdateStarted, AppType.Client,
                DateTimeOffset.UtcNow);

            Assert.Equal(UpdateEvent.UpdateStarted, report.Event);
            Assert.Equal("MyApp.exe", report.AppName);
            Assert.Equal("1.0.0", report.FromVersion);
            Assert.Equal("2.0.0", report.ToVersion);
            Assert.Equal(AppType.Client, report.AppType);
        }

        [Fact]
        public void UpdateReport_FailedWithError()
        {
            var report = new UpdateReport("MyApp.exe", "1.0.0", "2.0.0",
                UpdateEvent.UpdateFailed, AppType.Client,
                DateTimeOffset.UtcNow,
                ErrorMessage: "Disk space insufficient",
                DurationMs: 15000.0);

            Assert.Equal(UpdateEvent.UpdateFailed, report.Event);
            Assert.Equal("Disk space insufficient", report.ErrorMessage);
            Assert.Equal(15000.0, report.DurationMs);
        }

        [Fact]
        public void UpdateReport_DownloadCompletedWithDuration()
        {
            var report = new UpdateReport("MyApp.exe", "1.0.0", "2.0.0",
                UpdateEvent.DownloadCompleted, AppType.Client,
                DateTimeOffset.UtcNow,
                DurationMs: 45200.5);

            Assert.Equal(UpdateEvent.DownloadCompleted, report.Event);
            Assert.Equal(45200.5, report.DurationMs);
        }

        [Fact]
        public void UpdateEvent_AllValues_AreDefined()
        {
            var values = Enum.GetValues<UpdateEvent>();
            Assert.Contains(UpdateEvent.UpdateStarted, values);
            Assert.Contains(UpdateEvent.DownloadCompleted, values);
            Assert.Contains(UpdateEvent.UpdateApplied, values);
            Assert.Contains(UpdateEvent.UpdateFailed, values);
            Assert.Contains(UpdateEvent.AppStarted, values);
        }

        #endregion

        #region IUpdateEventListener

        [Fact]
        public void UpdateEventListener_AllMethods_AreCallable()
        {
            var listener = new TestEventListener();
            var versionInfo = new VersionInfo { Version = "2.0.0", Url = "https://cdn.example.com/pkg.zip", Format = "ZIP" };

            listener.OnAllDownloadCompleted(new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
            listener.OnDownloadCompleted(new MultiDownloadCompletedEventArgs(versionInfo, true));
            listener.OnDownloadError(new MultiDownloadErrorEventArgs(new Exception("test"), versionInfo));
            listener.OnDownloadStatistics(new MultiDownloadStatisticsEventArgs(versionInfo, TimeSpan.Zero, "0 B/s", 0, 0, 0));
            listener.OnUpdateInfo(new UpdateInfoEventArgs(new VersionRespDTO { Code = 200 }));
            listener.OnException(new ExceptionEventArgs(new Exception("test"), "test"));
            listener.OnProgress(new ProgressEventArgs(new DownloadProgress("update.zip", 50L * 1024 * 1024, 100L * 1024 * 1024, 50.0, DownloadStatus.Downloading)));
                        Assert.True(listener.AllDownloadCalled);
            Assert.True(listener.DownloadCompletedCalled);
            Assert.True(listener.DownloadErrorCalled);
            Assert.True(listener.StatisticsCalled);
            Assert.True(listener.UpdateInfoCalled);
            Assert.True(listener.ExceptionCalled);
            Assert.True(listener.ProgressCalled);
                    }

        private sealed class TestEventListener : IUpdateEventListener
        {
            public bool AllDownloadCalled { get; private set; }
            public bool DownloadCompletedCalled { get; private set; }
            public bool DownloadErrorCalled { get; private set; }
            public bool StatisticsCalled { get; private set; }
            public bool UpdateInfoCalled { get; private set; }
            public bool ExceptionCalled { get; private set; }
            public bool ProgressCalled { get; private set; }
            public bool CustomEventCalled { get; private set; }

            public void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs e) => AllDownloadCalled = true;
            public void OnDownloadCompleted(MultiDownloadCompletedEventArgs e) => DownloadCompletedCalled = true;
            public void OnDownloadError(MultiDownloadErrorEventArgs e) => DownloadErrorCalled = true;
            public void OnDownloadStatistics(MultiDownloadStatisticsEventArgs e) => StatisticsCalled = true;
            public void OnUpdateInfo(UpdateInfoEventArgs e) => UpdateInfoCalled = true;
            public void OnException(ExceptionEventArgs e) => ExceptionCalled = true;
            public void OnProgress(ProgressEventArgs e) => ProgressCalled = true;
            public void OnCustomEvent(string eventName, EventArgs e) => CustomEventCalled = true;
        }

        #endregion

        #region Security Extensibility

        [Fact]
        public void AuthSchemeAndToken_CanBeConfigured()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.Scheme, "Bearer")
                .Option(UpdateOptions.Token, "jwt-token-abc123")
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://api.example.com",
                    MainAppName = "MyApp.exe",
                    ClientVersion = "1.0.0",
                    InstallPath = _testDir,
                    AppSecretKey = "key",
                    Scheme = "Bearer",
                    Token = "jwt-token-abc123"
                });
            Assert.NotNull(b);
        }

        [Fact]
        public void AllAuthSchemes_CanBeConfigured()
        {
            foreach (var scheme in new[] { "Bearer", "ApiKey", "Basic", "HMAC" })
            {
                var b = new GeneralUpdateBootstrap()
                    .Option(UpdateOptions.Scheme, scheme)
                    .Option(UpdateOptions.Token, "test-token")
                    .SetConfig(new Configinfo
                    {
                        UpdateUrl = "https://api.example.com",
                        MainAppName = "MyApp.exe",
                        ClientVersion = "1.0.0",
                        InstallPath = _testDir,
                        AppSecretKey = "key",
                        Scheme = scheme,
                        Token = "test-token"
                    });
                Assert.NotNull(b);
            }
        }

        #endregion

        #region Full Extension Chain

        [Fact]
        public void Bootstrap_FullExtensions_AllConfigured()
        {
            var b = new GeneralUpdateBootstrap()
                .Option(UpdateOptions.AppType, AppType.Client)
                .Option(UpdateOptions.UpdateUrl, "https://update.example.com/api")
                .Option(UpdateOptions.ReportUrl, "https://telemetry.example.com/report")
                .Option(UpdateOptions.Scheme, "Bearer")
                .Option(UpdateOptions.Token, "jwt-token")
                .Option(UpdateOptions.PermissionScript, "#!/bin/bash\nchmod +x /opt/app/Update")
                .SetConfig(new Configinfo
                {
                    UpdateUrl = "https://update.example.com/api",
                    MainAppName = "MyApp.exe",
                    ClientVersion = "1.0.0",
                    InstallPath = _testDir,
                    AppSecretKey = "key",
                    Scheme = "Bearer",
                    Token = "jwt-token",
                    ReportUrl = "https://telemetry.example.com/report"
                })
                .AddListenerUpdateInfo((s, e) => { })
                .AddListenerException((s, e) => { })
                .AddListenerMultiAllDownloadCompleted((s, e) => { })
                .AddListenerUpdatePrecheck(args => false)
                .AddCustomOption(new List<Func<bool>> { () => true });

            Assert.NotNull(b);
        }

        #endregion
    }
}
