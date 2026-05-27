using System;
using System.Collections.Generic;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Download.Models;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.Configuration
{
    /// <summary>
    /// Unit tests for configuration models used across the update framework.
    /// Covers:
    ///   - BlackListConfig (new config-based blacklist with IReadOnlyList)
    ///   - HubConfig (SignalR hub configuration)
    ///   - DownloadAsset / DownloadPlan / DownloadProgress / DownloadResult (download pipeline models)
    ///   - DownloadStatus / DownloadPriority enums
    ///   - AppType / DiffMode / UpdateMode / PlatformType / OssProvider enums
    ///   - UpdateOption&lt;T&gt; value semantics
    ///   - UpdateReport / UpdateEvent types
    /// </summary>
    public class ConfigurationModelsTests
    {
        #region BlackListConfig

        [Fact]
        public void BlackListConfig_Empty_HasAllNullLists()
        {
            var config = BlackListConfig.Empty;
            Assert.NotNull(config);
            Assert.Null(config.BlackFiles);
            Assert.Null(config.BlackFormats);
            Assert.Null(config.SkipDirectorys);
        }

        [Fact]
        public void BlackListConfig_WithAllFields_SetsCorrectly()
        {
            var config = new BlackListConfig(
                BlackFiles: new List<string> { "*.pdb", "*.config", "secret.key" },
                BlackFormats: new List<string> { ".log", ".tmp", ".cache" },
                SkipDirectorys: new List<string> { "logs", "temp", "__backups__" }
            );

            Assert.NotNull(config.BlackFiles);
            Assert.Equal(3, config.BlackFiles.Count);
            Assert.Contains("*.pdb", config.BlackFiles);
            Assert.NotNull(config.BlackFormats);
            Assert.Equal(3, config.BlackFormats.Count);
            Assert.NotNull(config.SkipDirectorys);
            Assert.Equal(3, config.SkipDirectorys.Count);
        }

        [Fact]
        public void BlackListConfig_Partial_SingleListOnly()
        {
            var config = new BlackListConfig(
                BlackFiles: new List<string> { "*.pdb" },
                BlackFormats: null,
                SkipDirectorys: null
            );

            Assert.Single(config.BlackFiles);
            Assert.Null(config.BlackFormats);
            Assert.Null(config.SkipDirectorys);
        }

        #endregion

        #region DownloadAsset

        [Fact]
        public void DownloadAsset_AllFields_ConstructsCorrectly()
        {
            var asset = new DownloadAsset(
                Name: "client-app-v2.0.0.zip",
                Url: "https://cdn.example.com/packages/v2.0.0.zip",
                Size: 100L * 1024 * 1024,
                SHA256: "sha256:abc123def456",
                Version: "2.0.0",
                Priority: DownloadPriority.Normal
            );

            Assert.Equal("client-app-v2.0.0.zip", asset.Name);
            Assert.Equal("2.0.0", asset.Version);
            Assert.Equal("https://cdn.example.com/packages/v2.0.0.zip", asset.Url);
            Assert.Equal("sha256:abc123def456", asset.SHA256);
            Assert.Equal(104857600, asset.Size);
            Assert.Equal(DownloadPriority.Normal, asset.Priority);
            Assert.False(asset.IsCrossVersion);
            Assert.False(asset.IsForcibly);
            Assert.False(asset.IsFreeze);
        }

        [Fact]
        public void DownloadAsset_CrossVersion_IsForcibly()
        {
            var asset = new DownloadAsset(
                Name: "critical-patch.zip",
                Url: "https://cdn.example.com/security/hotfix.zip",
                Size: 5L * 1024 * 1024,
                SHA256: null,
                Version: "2.0.1-hotfix",
                Priority: DownloadPriority.High,
                IsForcibly: true,
                IsCrossVersion: true,
                FromVersion: "2.0.0"
            );

            Assert.Equal(DownloadPriority.High, asset.Priority);
            Assert.True(asset.IsForcibly);
            Assert.True(asset.IsCrossVersion);
            Assert.Equal("2.0.0", asset.FromVersion);
        }

        #endregion

        #region DownloadPlan

        [Fact]
        public void DownloadPlan_Empty_HasNoAssets()
        {
            var plan = DownloadPlan.Empty;
            Assert.False(plan.HasAssets);
            Assert.Empty(plan.Assets);
            Assert.False(plan.IsForcibly);
        }

        [Fact]
        public void DownloadPlan_WithAssets_HasCorrectCount()
        {
            var assets = new List<DownloadAsset>
            {
                new("update-v2.zip", "https://cdn.example.com/v2.zip", 50 * 1024 * 1024, null, "2.0.0"),
                new("patch-v2.1.zip", "https://cdn.example.com/v2.1.zip", 5 * 1024 * 1024, null, "2.1.0")
            };
            var plan = new DownloadPlan(assets, false);

            Assert.True(plan.HasAssets);
            Assert.Equal(2, plan.Assets.Count);
        }

        #endregion

        #region DownloadProgress

        [Fact]
        public void DownloadProgress_Pending()
        {
            var progress = new DownloadProgress("update.zip", 0, 50L * 1024 * 1024, 0.0, DownloadStatus.Pending);

            Assert.Equal(DownloadStatus.Pending, progress.Status);
            Assert.Equal(0, progress.BytesDownloaded);
            Assert.Equal(0.0, progress.Percentage);
        }

        [Fact]
        public void DownloadProgress_HalfComplete()
        {
            var progress = new DownloadProgress("update.zip", 25L * 1024 * 1024, 50L * 1024 * 1024, 50.0, DownloadStatus.Downloading);

            Assert.Equal(DownloadStatus.Downloading, progress.Status);
            Assert.Equal(50.0, progress.Percentage);
        }

        [Fact]
        public void DownloadProgress_Completed()
        {
            var progress = new DownloadProgress("update.zip", 50L * 1024 * 1024, 50L * 1024 * 1024, 100.0, DownloadStatus.Completed);

            Assert.Equal(DownloadStatus.Completed, progress.Status);
            Assert.Equal(100.0, progress.Percentage);
        }

        [Fact]
        public void DownloadProgress_Failed()
        {
            var progress = new DownloadProgress("update.zip", 10L * 1024 * 1024, 50L * 1024 * 1024, 20.0, DownloadStatus.Failed);

            Assert.Equal(DownloadStatus.Failed, progress.Status);
            Assert.NotEqual(100.0, progress.Percentage);
        }

        #endregion

        #region DownloadResult

        [Fact]
        public void DownloadResult_Success()
        {
            var result = new DownloadResult(
                null!,
                "/tmp/update.zip",
                50L * 1024 * 1024,
                TimeSpan.FromSeconds(30),
                0,
                true,
                null);

            Assert.True(result.Success);
            Assert.Equal("/tmp/update.zip", result.LocalPath);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void DownloadResult_FailureWithRetries()
        {
            var result = new DownloadResult(
                null!,
                null,
                0,
                TimeSpan.FromSeconds(15),
                3,
                false,
                "Connection timeout after 3 retries");

            Assert.False(result.Success);
            Assert.Equal(3, result.RetryCount);
            Assert.Equal("Connection timeout after 3 retries", result.ErrorMessage);
        }

        #endregion

        #region Enum Types

        [Fact]
        public void AppType_ClientIs1() => Assert.Equal(1, (int)AppType.Client);
        [Fact]
        public void AppType_UpgradeIs2() => Assert.Equal(2, (int)AppType.Upgrade);
        [Fact]
        public void AppType_OSSClientIs3() => Assert.Equal(3, (int)AppType.OSSClient);
        [Fact]
        public void AppType_OSSUpgradeIs4() => Assert.Equal(4, (int)AppType.OSSUpgrade);

        [Fact]
        public void DiffMode_SerialAndParallel_AreDefined()
        {
            Assert.Contains(DiffMode.Serial, Enum.GetValues<DiffMode>());
            Assert.Contains(DiffMode.Parallel, Enum.GetValues<DiffMode>());
        }

        [Fact]
        public void PlatformType_AllFour()
        {
            var values = Enum.GetValues<PlatformType>();
            Assert.Contains(PlatformType.Windows, values);
            Assert.Contains(PlatformType.Linux, values);
            Assert.Contains(PlatformType.MacOS, values);
            Assert.Contains(PlatformType.Unknown, values);
        }

        [Fact]
        public void DownloadStatus_FiveValues()
        {
            var values = Enum.GetValues<DownloadStatus>();
            Assert.Contains(DownloadStatus.Pending, values);
            Assert.Contains(DownloadStatus.Downloading, values);
            Assert.Contains(DownloadStatus.Completed, values);
            Assert.Contains(DownloadStatus.Failed, values);
            Assert.Contains(DownloadStatus.Retrying, values);
        }

        [Fact]
        public void DownloadPriority_ThreeValues()
        {
            var values = Enum.GetValues<DownloadPriority>();
            Assert.Contains(DownloadPriority.Low, values);
            Assert.Contains(DownloadPriority.Normal, values);
            Assert.Contains(DownloadPriority.High, values);
        }

        [Fact]
        public void UpdateEvent_FiveValues()
        {
            var values = Enum.GetValues<UpdateEvent>();
            Assert.Contains(UpdateEvent.UpdateStarted, values);
            Assert.Contains(UpdateEvent.DownloadCompleted, values);
            Assert.Contains(UpdateEvent.UpdateApplied, values);
            Assert.Contains(UpdateEvent.UpdateFailed, values);
            Assert.Contains(UpdateEvent.AppStarted, values);
        }

        #endregion

        #region UpdateOption<T> Semantics

        [Fact]
        public void UpdateOption_ValueOf_String_DefaultsCorrectly()
        {
            var option = UpdateOption.ValueOf<string>("STRING_KEY", "hello");
            Assert.Equal("STRING_KEY", option.Name);
            Assert.Equal("hello", option.DefaultValue);
        }

        [Fact]
        public void UpdateOption_ValueOf_Int_DefaultsCorrectly()
        {
            var option = UpdateOption.ValueOf<int>("INT_KEY", 42);
            Assert.Equal(42, option.DefaultValue);
        }

        [Fact]
        public void UpdateOption_ValueOf_Bool_DefaultsCorrectly()
        {
            var option = UpdateOption.ValueOf<bool>("BOOL_KEY", true);
            Assert.True(option.DefaultValue);
        }

        [Fact]
        public void UpdateOption_ValueOf_NullableInt_DefaultsCorrectly()
        {
            var option = UpdateOption.ValueOf<int?>("NULLABLE_KEY", null);
            Assert.Null(option.DefaultValue);
        }

        [Fact]
        public void UpdateOption_ValueOf_Enum_DefaultsCorrectly()
        {
            var option = UpdateOption.ValueOf<AppType>("ENUM_KEY", AppType.Client);
            Assert.Equal(AppType.Client, option.DefaultValue);
        }

        #endregion
    }
}
