using Moq;
using GeneralUpdate.Differential;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest
{
    /// <summary>
    /// Behavioral tests for <see cref="DifferentialCore"/> static facade methods.
    /// Covers all overloads of Clean and Dirty, custom differ/strategy injection,
    /// and full round-trip integration.
    /// </summary>
    public class DifferentialCoreTests : IDisposable
    {
        private readonly string _testDir;

        public DifferentialCoreTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DifferentialCoreTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact(DisplayName = "Clean_仅三参数调用_使用默认策略生成补丁文件")]
        public async Task Clean_ThreeParamOverload_GeneratesPatches()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var fileName = "test.dll";
            File.WriteAllText(Path.Combine(sourceDir, fileName), "original file content");
            File.WriteAllText(Path.Combine(targetDir, fileName), "modified file content with changes");

            // Act
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir);

            // Assert
            var patchFiles = Directory.GetFiles(patchDir, "*.patch", SearchOption.AllDirectories);
            Assert.NotEmpty(patchFiles);
        }

        [Fact(DisplayName = "Clean_指定自定义IBinaryDiffer_调用自定义CleanAsync")]
        public async Task Clean_FiveParamWithCustomDiffer_UsesCustomDiffer()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(patchDir);

            var fileName = "app.dll";
            File.WriteAllText(Path.Combine(sourceDir, fileName), "old binary content");
            File.WriteAllText(Path.Combine(targetDir, fileName), "new binary content modified");

            var mockDiffer = new Mock<IBinaryDiffer>();
            mockDiffer
                .Setup(d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir,
                binaryDiffer: mockDiffer.Object);

            // Assert
            mockDiffer.Verify(
                d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        [Fact(DisplayName = "Clean_同时传入自定义策略和Differ_使用策略并忽略Differ")]
        public async Task Clean_WithCustomStrategy_UsesStrategyAndIgnoresDiffer()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");

            var mockStrategy = new Mock<ICleanStrategy>();
            mockStrategy
                .Setup(s => s.ExecuteAsync(sourceDir, targetDir, patchDir))
                .Returns(Task.CompletedTask);

            var mockDiffer = new Mock<IBinaryDiffer>();

            // Act
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir,
                binaryDiffer: mockDiffer.Object,
                strategy: mockStrategy.Object);

            // Assert
            mockStrategy.Verify(s => s.ExecuteAsync(sourceDir, targetDir, patchDir), Times.Once());
            mockDiffer.Verify(
                d => d.CleanAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact(DisplayName = "Clean_向后兼容重载_传入ICleanStrategy参数_正常工作")]
        public async Task Clean_BackwardCompatOverload_Works()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var patchDir = Path.Combine(_testDir, "patch");

            var mockStrategy = new Mock<ICleanStrategy>();
            mockStrategy
                .Setup(s => s.ExecuteAsync(sourceDir, targetDir, patchDir))
                .Returns(Task.CompletedTask);

            // Act — 4-param backward-compat overload (strategy as 4th arg)
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir,
                strategy: mockStrategy.Object);

            // Assert
            mockStrategy.Verify(s => s.ExecuteAsync(sourceDir, targetDir, patchDir), Times.Once());
        }

        [Fact(DisplayName = "Dirty_仅双参数调用_使用默认策略应用补丁")]
        public async Task Dirty_TwoParamOverload_AppliesPatches()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            var fileName = "main.dll";
            var oldContent = "v1 content - old version";
            var newContent = "v2 content - updated version with more data";

            // Old version in source and app dirs
            File.WriteAllText(Path.Combine(sourceDir, fileName), oldContent);
            File.WriteAllText(Path.Combine(appDir, fileName), oldContent);
            // New version in target dir
            File.WriteAllText(Path.Combine(targetDir, fileName), newContent);

            // Step 1: Generate the patch
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir);

            // Act — apply the patch
            await DifferentialCore.Dirty(appDir, patchDir);

            // Assert — app should now match the target (new version)
            Assert.True(File.Exists(Path.Combine(appDir, fileName)));
            var result = File.ReadAllText(Path.Combine(appDir, fileName));
            Assert.Equal(newContent, result);
        }

        [Fact(DisplayName = "Dirty_传入自定义IDirtyStrategy_调用自定义策略")]
        public async Task Dirty_WithCustomStrategy_UsesStrategy()
        {
            // Arrange
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");

            var mockStrategy = new Mock<IDirtyStrategy>();
            mockStrategy
                .Setup(s => s.ExecuteAsync(appDir, patchDir))
                .Returns(Task.CompletedTask);

            // Act — 4-param overload with strategy
            await DifferentialCore.Dirty(appDir, patchDir, strategy: mockStrategy.Object);

            // Assert
            mockStrategy.Verify(s => s.ExecuteAsync(appDir, patchDir), Times.Once());
        }

        [Fact(DisplayName = "Dirty_向后兼容重载_传入IDirtyStrategy参数_正常工作")]
        public async Task Dirty_BackwardCompatOverload_Works()
        {
            // Arrange
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");

            var mockStrategy = new Mock<IDirtyStrategy>();
            mockStrategy
                .Setup(s => s.ExecuteAsync(appDir, patchDir))
                .Returns(Task.CompletedTask);

            // Act — 3-param backward-compat overload (strategy as 3rd arg, positional)
            await DifferentialCore.Dirty(appDir, patchDir, mockStrategy.Object);

            // Assert
            mockStrategy.Verify(s => s.ExecuteAsync(appDir, patchDir), Times.Once());
        }

        [Fact(DisplayName = "Clean后Dirty_完整往返_多个文件生成相同内容")]
        public async Task CleanThenDirty_RoundTrip_ProducesIdenticalFiles()
        {
            // Arrange
            var sourceDir = Path.Combine(_testDir, "source");
            var targetDir = Path.Combine(_testDir, "target");
            var appDir = Path.Combine(_testDir, "app");
            var patchDir = Path.Combine(_testDir, "patch");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(patchDir);

            // Multiple files with distinct old/new content
            var files = new Dictionary<string, (string Old, string New)>
            {
                ["core.dll"] = ("core v1 payload data", "core v2 payload data with enhancements"),
                ["settings.xml"] = ("<settings><theme>light</theme></settings>",
                                    "<settings><theme>dark</theme><lang>zh-CN</lang></settings>"),
                ["assets.bin"] = ("binary v1 block", "binary v2 block updated"),
            };

            // Populate source and app with old content
            foreach (var (name, (old, _)) in files)
            {
                File.WriteAllText(Path.Combine(sourceDir, name), old);
                File.WriteAllText(Path.Combine(appDir, name), old);
            }

            // Populate target with new content
            foreach (var (name, (_, @new)) in files)
            {
                File.WriteAllText(Path.Combine(targetDir, name), @new);
            }

            // Act
            await DifferentialCore.Clean(sourceDir, targetDir, patchDir);
            await DifferentialCore.Dirty(appDir, patchDir);

            // Assert — every file in appDir now matches the target version
            foreach (var (name, (_, expected)) in files)
            {
                var resultPath = Path.Combine(appDir, name);
                Assert.True(File.Exists(resultPath), $"File '{name}' should exist after Dirty");
                Assert.Equal(expected, File.ReadAllText(resultPath));
            }
        }
    }
}
