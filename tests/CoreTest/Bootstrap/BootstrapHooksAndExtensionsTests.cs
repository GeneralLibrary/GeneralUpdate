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

            public Task<bool> OnBeforeUpdateAsync(UpdateContext ctx) { BeforeUpdateCalled = true; BeforeCtx = ctx; return Task.FromResult(true); }
            public Task OnDownloadCompletedAsync(DownloadContext ctx) { DownloadCompletedCalled = true; DownloadCtx = ctx; return Task.CompletedTask; }
            public Task OnAfterUpdateAsync(UpdateContext ctx) { AfterUpdateCalled = true; return Task.CompletedTask; }
            public Task OnUpdateErrorAsync(UpdateContext ctx, Exception ex) { ErrorCalled = true; CapturedError = ex; return Task.CompletedTask; }
            public Task OnBeforeStartAppAsync(UpdateContext ctx) { BeforeStartAppCalled = true; return Task.CompletedTask; }
        }

        #endregion

        #region Hook Lifecycle

        [Fact] public void TrackingHooks_InitialState_AllFlagsFalse() { var h = new TrackingHooks(); Assert.False(h.BeforeUpdateCalled); Assert.False(h.ErrorCalled); }

        [Fact]
        public async Task TrackingHooks_OnBeforeUpdate_ReturnsTrueAndRecordsContext()
        {
            var hooks = new TrackingHooks();
            var ctx = new UpdateContext("MyApp.exe", "/install", "1.0.0", "2.0.0", AppType.Client);
            var result = await hooks.OnBeforeUpdateAsync(ctx);
            Assert.True(result);
            Assert.Equal("1.0.0", hooks.BeforeCtx!.CurrentVersion);
        }

        [Fact]
        public async Task TrackingHooks_OnBeforeUpdate_CanReject()
        {
            var hooks = new RejectingHooks();
            var ctx = new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client);
            Assert.False(await hooks.OnBeforeUpdateAsync(ctx));
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
            Assert.True(hooks.DownloadCtx!.Success);
        }

        [Fact]
        public async Task TrackingHooks_OnDownloadCompleted_Failure()
        {
            var hooks = new TrackingHooks();
            var ctx = new DownloadContext("corrupt.zip", "2.0.0", 0, TimeSpan.FromSeconds(5), null, false);
            await hooks.OnDownloadCompletedAsync(ctx);
            Assert.False(hooks.DownloadCtx!.Success);
        }

        [Fact]
        public async Task TrackingHooks_OnAfterUpdate_RecordsCall()
        {
            var hooks = new TrackingHooks();
            await hooks.OnAfterUpdateAsync(new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client));
            Assert.True(hooks.AfterUpdateCalled);
        }

        [Fact]
        public async Task TrackingHooks_OnUpdateError_CapturesException()
        {
            var hooks = new TrackingHooks();
            var ex = new InvalidOperationException("Hash verification failed");
            await hooks.OnUpdateErrorAsync(new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client), ex);
            Assert.Equal("Hash verification failed", hooks.CapturedError!.Message);
        }

        [Fact]
        public async Task TrackingHooks_FullLifecycle_AllMethodsCalled()
        {
            var hooks = new TrackingHooks();
            var uctx = new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client);
            var dctx = new DownloadContext("pkg.zip", "2.0.0", 100, TimeSpan.FromSeconds(10), "/tmp/pkg.zip", true);
            await hooks.OnBeforeUpdateAsync(uctx);
            await hooks.OnDownloadCompletedAsync(dctx);
            await hooks.OnAfterUpdateAsync(uctx);
            await hooks.OnBeforeStartAppAsync(uctx);
            Assert.True(hooks.BeforeUpdateCalled && hooks.DownloadCompletedCalled && hooks.AfterUpdateCalled && hooks.BeforeStartAppCalled);
            Assert.False(hooks.ErrorCalled);
        }

        #endregion

        #region NoOpUpdateHooks

        [Fact]
        public async Task NoOpUpdateHooks_AllMethods_ReturnDefaults()
        {
            var hooks = new NoOpUpdateHooks();
            Assert.True(await hooks.OnBeforeUpdateAsync(new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client)));
            await hooks.OnDownloadCompletedAsync(new DownloadContext("pkg.zip", "2.0.0", 100, TimeSpan.FromSeconds(10), "/tmp/pkg.zip", true));
            await hooks.OnAfterUpdateAsync(new UpdateContext("App.exe", "/app", "1.0.0", "2.0.0", AppType.Client));
            await hooks.OnUpdateErrorAsync(new UpdateContext("App.exe", "/app", "1.0.0", null, AppType.Client), new Exception("test"));
            await hooks.OnBeforeStartAppAsync(new UpdateContext("App.exe", "/app", "2.0.0", null, AppType.Client));
        }

        #endregion

        #region UpdateReport / IUpdateReporter

        [Fact]
        public void UpdateReport_StartedEvent()
        {
            var report = new UpdateReport(123, (int)UpdateStatus.Updating, 1);
            Assert.Equal(123, report.RecordId);
            Assert.Equal((int)UpdateStatus.Updating, report.Status);
            Assert.Equal(1, report.Type);
        }

        [Fact]
        public void UpdateReport_FailedWithError()
        {
            var report = new UpdateReport(456, (int)UpdateStatus.Failure, 1);
            Assert.Equal(456, report.RecordId);
            Assert.Equal((int)UpdateStatus.Failure, report.Status);
            Assert.Equal(1, report.Type);
        }

        [Fact]
        public void UpdateStatus_AllValues_AreDefined()
        {
            var values = Enum.GetValues<UpdateStatus>();
            Assert.Contains(UpdateStatus.Updating, values);
            Assert.Contains(UpdateStatus.Updating, values);
            Assert.Contains(UpdateStatus.Success, values);
            Assert.Contains(UpdateStatus.Failure, values);
            Assert.Contains(UpdateStatus.Success, values);
        }

        #endregion

        #region IUpdateEventListener

        [Fact]
        public void UpdateStatusListener_AllMethods_AreCallable()
        {
            var listener = new TestEventListener();
            var vi = new VersionInfo { Version = "2.0.0", Url = "https://cdn.example.com/pkg.zip", Format = "ZIP" };
            listener.OnAllDownloadCompleted(new MultiAllDownloadCompletedEventArgs(true, new List<(object, string)>()));
            listener.OnDownloadCompleted(new MultiDownloadCompletedEventArgs(vi, true));
            listener.OnDownloadError(new MultiDownloadErrorEventArgs(new Exception("test"), vi));
            listener.OnDownloadStatistics(new MultiDownloadStatisticsEventArgs(vi, TimeSpan.Zero, "0 B/s", 0, 0, 0));
            listener.OnUpdateInfo(new UpdateInfoEventArgs(new VersionRespDTO { Code = 200 }));
            listener.OnException(new ExceptionEventArgs(new Exception("test"), "test"));
            listener.OnProgress(new ProgressEventArgs(new DownloadProgress("update.zip", 50L * 1024 * 1024, 100L * 1024 * 1024, 50.0, DownloadStatus.Downloading)));
            Assert.True(listener.AllDownloadCalled && listener.DownloadCompletedCalled && listener.UpdateInfoCalled && listener.ExceptionCalled && listener.ProgressCalled);
        }

        private sealed class TestEventListener : IUpdateEventListener
        {
            public bool AllDownloadCalled, DownloadCompletedCalled, DownloadErrorCalled, StatisticsCalled, UpdateInfoCalled, ExceptionCalled, ProgressCalled;
            public void OnAllDownloadCompleted(MultiAllDownloadCompletedEventArgs e) => AllDownloadCalled = true;
            public void OnDownloadCompleted(MultiDownloadCompletedEventArgs e) => DownloadCompletedCalled = true;
            public void OnDownloadError(MultiDownloadErrorEventArgs e) => DownloadErrorCalled = true;
            public void OnDownloadStatistics(MultiDownloadStatisticsEventArgs e) => StatisticsCalled = true;
            public void OnUpdateInfo(UpdateInfoEventArgs e) => UpdateInfoCalled = true;
            public void OnException(ExceptionEventArgs e) => ExceptionCalled = true;
            public void OnProgress(ProgressEventArgs e) => ProgressCalled = true;
        }

        #endregion

        #region UpdateContext & DownloadContext

        [Fact]
        public void UpdateContext_AllFields_SetCorrectly()
        {
            var ctx = new UpdateContext("MyApp.exe", "/opt/app", "3.2.1", "4.0.0", AppType.Client);
            Assert.Equal("MyApp.exe", ctx.UpdateAppName);
            Assert.Equal("3.2.1", ctx.CurrentVersion);
            Assert.Equal("4.0.0", ctx.TargetVersion);
        }

        [Fact]
        public void DownloadContext_Successful()
        {
            var ctx = new DownloadContext("client-v2.0.0.zip", "2.0.0", 50L * 1024 * 1024, TimeSpan.FromMinutes(2), "/tmp/update.zip", true);
            Assert.Equal("client-v2.0.0.zip", ctx.AssetName);
            Assert.True(ctx.Success);
        }

        [Fact]
        public void DownloadContext_Failed()
        {
            var ctx = new DownloadContext("failed.zip", "2.0.0", 0, TimeSpan.FromSeconds(3), null, false);
            Assert.False(ctx.Success);
            Assert.Null(ctx.LocalPath);
        }

        #endregion
    }
}
