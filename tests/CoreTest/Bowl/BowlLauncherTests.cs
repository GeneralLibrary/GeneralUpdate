using GeneralUpdate.Core.Bowl;

namespace CoreTest.Bowl;

/// <summary>
///     Bowl launch contract tests. The CLI string and the single-instance key ARE the Core ↔
///     Bowl coupling surface, so their exact shape is pinned here (the watchdog console mirrors
///     the same algorithms in Bowl.Console.Tests).
/// </summary>
public sealed class BowlLauncherTests
{
    [Fact]
    public async Task EnsureRunningAsync_MissingExecutable_ReturnsFalseWithoutSideEffects()
    {
        var started = await BowlLauncher.EnsureRunningAsync(new BowlLaunchOptions
        {
            BowlExecutablePath = @"Z:\nowhere\bowl.exe",
            TargetProcessName = "MyApp",
            InstallPath = AppContext.BaseDirectory,
            Version = "1.0.2",
        });

        // Documented "Bowl not installed" case: log + false, everything else keeps working.
        Assert.False(started);
    }

    [Fact]
    public async Task EnsureRunningAsync_MissingTargetName_ReturnsFalse()
    {
        var started = await BowlLauncher.EnsureRunningAsync(new BowlLaunchOptions
        {
            BowlExecutablePath = AssemblyLocation(),
            InstallPath = AppContext.BaseDirectory,
            Version = "1.0.2",
        });

        Assert.False(started);
    }

    [Fact]
    public void BuildInstanceKey_DeterministicAnd32HexChars()
    {
        var first = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp");
        var second = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp");

        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void BuildInstanceKey_TrailingSeparatorIsNormalized()
    {
        var withSeparator = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp\");
        var withoutSeparator = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp");

        Assert.Equal(withoutSeparator, withSeparator);
    }

    /// <summary>Accessor for the internal key builder (contract is internal by design).</summary>
    [Fact]
    public void BuildInstanceKey_CrossImplementationContract_MatchesWatchdogAlgorithm()
    {
        // Independent re-computation of the documented algorithm — guards against a silent
        // change that would break the mutex handshake with the watchdog console.
        var target = "MyApp";
        var install = @"D:\apps\MyApp";

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
            $"GeneralUpdate.Bowl.v2|{target.ToLowerInvariant()}|{install.ToLowerInvariant()}"));
        var expected = Convert.ToHexString(hash).ToLowerInvariant()[..32];

        Assert.Equal(expected, BowlLauncher.BuildInstanceKey(target, install));
    }

    [Fact]
    public void BuildMutexName_MatchesWatchdogContract()
    {
        // The watchdog console creates `Global\GeneralUpdate.Bowl.{key}`
        // (SingleInstanceLock.Acquire) — the probe must open exactly this name.
        var key = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp");

        Assert.Equal($"Global\\GeneralUpdate.Bowl.{key}", BowlLauncher.BuildMutexName(key));
    }

    [Fact]
    public void IsAlreadyRunning_FlipsWithTheWatchdogMutex()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var key = BowlLauncher.BuildInstanceKey("MyApp", @"D:\apps\MyApp");
        var mutexName = BowlLauncher.BuildMutexName(key);

        // Not held → not running.
        Assert.False(BowlLauncher.IsAlreadyRunning(key));

        // Held under the watchdog's exact name → running (this is the handshake the
        // probe exists for; a bare-key probe here would always report false).
        var watchdogMutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        try
        {
            if (!createdNew)
                return; // A real watchdog owns the name on this machine — probe semantics unchanged.

            Assert.True(BowlLauncher.IsAlreadyRunning(key));
            watchdogMutex.ReleaseMutex();
        }
        finally
        {
            // Closing the last handle destroys the unowned named mutex — the kernel makes
            // the name disappear only then, so this MUST precede the final assertion.
            watchdogMutex.Dispose();
        }

        Assert.False(BowlLauncher.IsAlreadyRunning(key));
    }

    private static string AssemblyLocation() =>
        System.Reflection.Assembly.GetExecutingAssembly().Location;
}
