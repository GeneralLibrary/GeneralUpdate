using System.Diagnostics;

namespace CoreTest.Compress;

public sealed class ControlledWorkerEntryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsWithoutProductionAuthorizationAndLeavesStateUntouched(bool validLookingArguments)
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-worker-denied-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "existing.txt");
            File.WriteAllText(file, "user data");
            var dll = Path.Combine(AppContext.BaseDirectory, "ControlledWorker", "GeneralUpdate.ControlledWorker.dll");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(dll);
            if (validLookingArguments)
            {
                foreach (var arg in new[] { "--bowl-controlled", "--attempt", Guid.NewGuid().ToString("D"), "--state-root", root })
                    start.ArgumentList.Add(arg);
            }
            using var worker = Process.Start(start) ?? throw new InvalidOperationException("Worker did not start.");
            var stdout = worker.StandardOutput.ReadToEndAsync();
            var stderr = worker.StandardError.ReadToEndAsync();
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(2, worker.ExitCode);
            Assert.Empty(await stdout);
            Assert.Contains("authorization is not implemented", await stderr);
            Assert.Equal("user data", File.ReadAllText(file));
            Assert.Single(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
