using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategies;
using Xunit.Abstractions;

namespace BowlTest.Integration
{
    /// <summary>
    /// End-to-end crash simulation tests that produce real output files.
    /// Uses mock strategies to simulate procdump output so tests run
    /// on any machine without requiring procdump binaries.
    /// </summary>
    public class BowlCrashPipelineTests : IDisposable
    {
        private readonly string _testBasePath;
        private readonly ITestOutputHelper _output;

        public BowlCrashPipelineTests(ITestOutputHelper output)
        {
            _output = output;
            _testBasePath = Path.Combine(Path.GetTempPath(), $"BowlCrashTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testBasePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                try { Directory.Delete(_testBasePath, recursive: true); }
                catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Simulates a full crash �?dump �?report �?callback pipeline.
        /// Produces real files that can be inspected on disk.
        /// </summary>
        [Fact]
        public async Task SimulatedCrash_ProducesDumpAndReportFiles()
        {
            // ---- Arrange ----
            var installPath = Path.Combine(_testBasePath, "MyApp");
            var backupDir = Path.Combine(installPath, "1.0.0");
            var failDir = Path.Combine(installPath, "fail", "2.0.0");
            var expectedDumpFile = "2.0.0_fail.dmp";
            var expectedReportFile = "2.0.0_fail.json";
            var expectedDumpPath = Path.Combine(failDir, expectedDumpFile);
            var expectedReportPath = Path.Combine(failDir, expectedReportFile);

            // Create backup directory with some fake backup files
            Directory.CreateDirectory(backupDir);
            File.WriteAllText(Path.Combine(backupDir, "MyApp.dll"), "v1.0.0 backup");

            // The mock strategy will simulate procdump writing a dump file
            var mockStrategy = new MockCrashStrategy(expectedDumpPath, new[]
            {
                "[10:00:01] Waiting for process 'MyApp.exe' to start...",
                "[10:00:02] Process started. Attaching...",
                "[10:00:02] Dump count reached. Writing dump file...",
                "[10:00:03] Dump written.",
                "[10:00:03] Dump count reached.",
                "[10:00:03] Dump 1 initiated: " + expectedDumpPath,
            });

            CrashInfo? capturedCrash = null;
            var callbackFired = new TaskCompletionSource<bool>();

            var context = new BowlContext
            {
                ProcessNameOrId = "MyApp.exe",
                DumpFileName = expectedDumpFile,
                FailFileName = expectedReportFile,
                TargetPath = installPath,
                FailDirectory = failDir,
                BackupDirectory = backupDir,
                WorkModel = "Upgrade",
                ExtendedField = "2.0.0",
                TimeoutMs = 5_000,
                DumpType = DumpType.Full,
                AutoRestore = true,
                OnCrash = (info, ct) =>
                {
                    capturedCrash = info;
                    callbackFired.TrySetResult(true);
                    return Task.CompletedTask;
                },
            };

            var bowl = CreateBowl(mockStrategy);

            _output.WriteLine("=== Test Setup ===");
            _output.WriteLine($"Install:  {installPath}");
            _output.WriteLine($"Backup:   {backupDir}");
            _output.WriteLine($"Fail:     {failDir}");
            _output.WriteLine($"Dump:     {expectedDumpPath}");
            _output.WriteLine($"Report:   {expectedReportPath}");
            _output.WriteLine("");

            // ---- Act ----
            _output.WriteLine("=== Launching Bowl ===");
            BowlResult result;
            try
            {
                result = await bowl.LaunchAsync(context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                throw;
            }

            _output.WriteLine("");
            _output.WriteLine("=== Results ===");
            _output.WriteLine($"Success:        {result.Success}");
            _output.WriteLine($"ExitCode:       {result.ExitCode}");
            _output.WriteLine($"DumpCaptured:   {result.DumpCaptured}");
            _output.WriteLine($"DumpFilePath:   {result.DumpFilePath}");
            _output.WriteLine($"CrashReport:    {result.CrashReportPath}");
            _output.WriteLine($"Restored:       {result.Restored}");

            // ---- Assert ----

            // 1. Crash was detected
            Assert.False(result.Success);
            Assert.True(result.DumpCaptured, "Dump should be captured");
            Assert.True(result.Restored, "Backup should be restored in Upgrade mode");

            // 2. Dump file exists
            Assert.True(File.Exists(expectedDumpPath),
                $"Dump file should exist at {expectedDumpPath}");

            // 3. Crash report exists and is valid JSON
            Assert.True(File.Exists(expectedReportPath),
                $"Crash report should exist at {expectedReportPath}");

            var reportContent = File.ReadAllText(expectedReportPath);
            Assert.NotEmpty(reportContent);
            Assert.Contains("2.0.0", reportContent);
            Assert.Contains("MyApp.exe", reportContent);
            Assert.Contains("ProcdumpOutPutLines", reportContent);

            _output.WriteLine("");
            _output.WriteLine("=== Crash Report Content ===");
            _output.WriteLine(reportContent);

            // 4. OnCrash callback was fired with correct data
            Assert.True(callbackFired.Task.IsCompleted, "OnCrash callback should have fired");
            Assert.NotNull(capturedCrash);
            Assert.Equal(expectedDumpPath, capturedCrash!.Value.DumpFilePath);
            Assert.Equal(expectedReportPath, capturedCrash.Value.CrashReportPath);
            Assert.Equal("2.0.0", capturedCrash.Value.Version);

            _output.WriteLine("");
            _output.WriteLine("=== Callback Data ===");
            _output.WriteLine($"  Dump:     {capturedCrash.Value.DumpFilePath}");
            _output.WriteLine($"  Report:   {capturedCrash.Value.CrashReportPath}");
            _output.WriteLine($"  Version:  {capturedCrash.Value.Version}");
            _output.WriteLine($"  ExitCode: {capturedCrash.Value.ExitCode}");

            // 5. Fail directory contains all expected files
            var failFiles = Directory.GetFiles(failDir);
            _output.WriteLine("");
            _output.WriteLine("=== Files in fail directory ===");
            foreach (var f in failFiles)
                _output.WriteLine($"  {f} ({new FileInfo(f).Length} bytes)");

            Assert.Contains(failFiles, f => f.EndsWith(".dmp"));
            Assert.Contains(failFiles, f => f.EndsWith(".json"));
        }

        /// <summary>
        /// Simulates a crash in Normal mode (no restore, no env var).
        /// </summary>
        [Fact]
        public async Task SimulatedCrash_NormalMode_DoesNotRestore()
        {
            var installPath = Path.Combine(_testBasePath, "NormalModeApp");
            var failDir = Path.Combine(installPath, "fail", "3.0.0");
            var dumpPath = Path.Combine(failDir, "3.0.0_fail.dmp");

            Directory.CreateDirectory(Path.Combine(installPath, "3.0.0"));
            File.WriteAllText(Path.Combine(installPath, "3.0.0", "some.dll"), "backup");

            var mockStrategy = new MockCrashStrategy(dumpPath, new[] { "Dump written." });

            var context = new BowlContext
            {
                ProcessNameOrId = "NormalApp.exe",
                DumpFileName = "3.0.0_fail.dmp",
                FailFileName = "3.0.0_fail.json",
                TargetPath = installPath,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(installPath, "3.0.0"),
                WorkModel = "Normal",
                ExtendedField = "3.0.0",
                TimeoutMs = 5_000,
                AutoRestore = true,
            };

            var bowl = CreateBowl(mockStrategy);
            var result = await bowl.LaunchAsync(context);

            _output.WriteLine($"Normal mode �?Restored: {result.Restored}, DumpCaptured: {result.DumpCaptured}");

            Assert.True(result.DumpCaptured);
            Assert.False(result.Restored, "Normal mode should NOT restore backup");
            Assert.True(File.Exists(dumpPath));
        }

        /// <summary>
        /// Verifies that when no dump is produced, the pipeline returns success
        /// and does NOT create a crash report.
        /// </summary>
        [Fact]
        public async Task NoCrash_NoDumpFile_ReturnsSuccess()
        {
            var installPath = Path.Combine(_testBasePath, "HealthyApp");
            var failDir = Path.Combine(installPath, "fail", "4.0.0");

            // Mock strategy that does NOT write a dump file (simulates healthy exit)
            var mockStrategy = new HealthyProcessStrategy();

            var context = new BowlContext
            {
                ProcessNameOrId = "HealthyApp.exe",
                DumpFileName = "4.0.0_fail.dmp",
                FailFileName = "4.0.0_fail.json",
                TargetPath = installPath,
                FailDirectory = failDir,
                BackupDirectory = Path.Combine(installPath, "4.0.0"),
                WorkModel = "Upgrade",
                ExtendedField = "4.0.0",
                TimeoutMs = 5_000,
            };

            var bowl = CreateBowl(mockStrategy);
            var result = await bowl.LaunchAsync(context);

            _output.WriteLine($"Healthy �?Success: {result.Success}, DumpCaptured: {result.DumpCaptured}");

            Assert.False(result.DumpCaptured, "No dump should be captured for healthy process");
            Assert.Equal(0, result.ExitCode);
        }

        // ---- Test Helpers ----

        private static Bowl CreateBowl(IBowlStrategy strategy)
        {
            return new Bowl(strategy, new CrashReporter(),
                new NoOpInfoProvider());
        }

        /// <summary>
        /// Mock strategy that simulates a crash by writing a fake dump file
        /// and returning procdump-like output lines.
        /// </summary>
        private sealed class MockCrashStrategy : IBowlStrategy
        {
            private readonly string _dumpPath;
            private readonly IReadOnlyList<string> _outputLines;

            public MockCrashStrategy(string dumpPath, IReadOnlyList<string> outputLines)
            {
                _dumpPath = dumpPath;
                _outputLines = outputLines;
            }

            public ProcessStartInfo? Prepare(in BowlContext context)
            {
                // Simulate procdump writing a dump file
                Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath)!);
                File.WriteAllText(_dumpPath, "FAKE_DUMP_CONTENT");
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit 0",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            public Task PostProcessAsync(in BowlContext context,
                ProcessExitResult exitResult, CancellationToken ct)
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Mock strategy that simulates a healthy exit (no dump).
        /// </summary>
        private sealed class HealthyProcessStrategy : IBowlStrategy
        {
            public ProcessStartInfo? Prepare(in BowlContext context)
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit 0",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            public Task PostProcessAsync(in BowlContext context,
                ProcessExitResult exitResult, CancellationToken ct)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoOpInfoProvider : ISystemInfoProvider
        {
            public Task ExportAsync(string outputDirectory, CancellationToken ct)
                => Task.CompletedTask;
        }

    }
}
