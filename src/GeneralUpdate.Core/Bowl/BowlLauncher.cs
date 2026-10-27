using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Bowl;

/// <summary>
///     Launches the independently installed Bowl watchdog from the main application's early
///     startup — the primary entry of the watchdog lifecycle ("whoever starts, starting twice
///     is harmless").
/// </summary>
/// <remarks>
///     <para>
///         <b>Why the main application launches Bowl:</b> only this entry covers the days with
///         no update at all. The updater-side launch (<c>WindowsStrategy</c> <c>LaunchBowl</c>)
///         is the deprecated compatibility path; the update-time kill of the watchdog stays in
///         <c>ClientStrategy.CallSmallBowlHomeAsync</c> (via the <c>Bowl</c> process name).
///     </para>
///     <para>
///         <b>Coupling surface:</b> CLI arguments + the single-instance key only — no IPC, no
///         shared state, no reference to the Bowl assembly. Duplicate launches are harmless:
///         the watchdog exits with code 2 when the single-instance lock is already held, and
///         this launcher probes the lock first to avoid a pointless spawn.
///     </para>
///     <para>
///         <b>Detached start:</b> on Windows the child is started with shell-execute and the
///         handle released immediately; on Unix a .NET child does not die with the parent.
///         Either way the watchdog survives a main-application crash — the hard requirement
///         for a watchdog.
///     </para>
/// </remarks>
public static class BowlLauncher
{
    /// <summary>Contract version of the launch arguments (mirrors the watchdog's <c>--schema</c>).</summary>
    internal const int ContractSchema = 2;

    /// <summary>
    ///     Starts the watchdog unless it is already running. Fire-and-forget and best-effort:
    ///     a failure is logged and reported as <c>false</c>, never thrown — the update /
    ///     self-check / diagnostics pipeline is unaffected by Bowl's absence.
    /// </summary>
    /// <param name="options">Launch parameters (executable path required).</param>
    /// <param name="ct">Optional cancellation of the (fast) launch sequence itself.</param>
    /// <returns>
    ///     <c>true</c> when the watchdog is confirmed running (already running or freshly
    ///     started); <c>false</c> when it could not be started.
    /// </returns>
    public static Task<bool> EnsureRunningAsync(BowlLaunchOptions options, CancellationToken ct = default)
        => Task.Run(() => EnsureRunning(options, ct), ct);

    private static bool EnsureRunning(BowlLaunchOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (options == null)
        {
            GeneralTracer.Warn("BowlLauncher: no launch options — watchdog not started.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(options.BowlExecutablePath) || !File.Exists(options.BowlExecutablePath))
        {
            // Documented "Bowl not installed" case: log + return false, no side effects.
            GeneralTracer.Info(
                $"BowlLauncher: watchdog executable not found ({options.BowlExecutablePath ?? "(empty)"}) — Bowl stays optional, continuing without it.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(options.TargetProcessName))
        {
            GeneralTracer.Warn("BowlLauncher: TargetProcessName is empty — watchdog not started.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(options.InstallPath))
        {
            GeneralTracer.Warn("BowlLauncher: InstallPath is empty — watchdog not started.");
            return false;
        }

        var instanceKey = BuildInstanceKey(options.TargetProcessName, options.InstallPath);
        if (IsAlreadyRunning(instanceKey))
        {
            GeneralTracer.Info($"BowlLauncher: watchdog already running ({instanceKey}) — nothing to do.");
            return true;
        }

        var arguments = BuildArguments(options);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = options.BowlExecutablePath,
                Arguments = arguments,
                // Windows: shell-execute detaches the child from this process. Unix: .NET
                // children do not die with the parent, and UseShellExecute=true is unsupported.
                UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                GeneralTracer.Warn("BowlLauncher: process start returned null — watchdog not started.");
                return false;
            }

            GeneralTracer.Info(
                $"BowlLauncher: watchdog started (PID: {process.Id}, target={options.TargetProcessName}, install={options.InstallPath}).");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"BowlLauncher: failed to start watchdog: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Builds the watchdog CLI (docs/bowl-redesign.md §2.2). Keep byte-compatible with the
    ///     watchdog's parser — this string IS the Core ↔ Bowl contract.
    /// </summary>
    private static string BuildArguments(BowlLaunchOptions options)
    {
        var builder = new StringBuilder("watch");
        builder.Append(" --target-name ").Append(Quote(options.TargetProcessName!));
        builder.Append(" --install-path ").Append(Quote(options.InstallPath));
        builder.Append(" --version ").Append(Quote(string.IsNullOrWhiteSpace(options.Version) ? "0.0.0" : options.Version!));
        builder.Append(" --schema ").Append(ContractSchema);
        builder.Append(" --dump-type ").Append(options.DumpType.ToString().ToLowerInvariant());
        builder.Append(" --idle-exit ").Append(options.IdleExit <= TimeSpan.Zero
            ? "00:30:00"
            : options.IdleExit.ToString(@"hh\:mm\:ss"));
        return builder.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(" ") ? $"\"{value}\"" : value;

    /// <summary>
    ///     Single-instance probe: try-OPEN the named mutex (never creates it) on Windows, or
    ///     attempts an exclusive open of the well-known lock file elsewhere. Best effort — a
    ///     wrong "not running" verdict is harmless because the watchdog itself exits with
    ///     code 2 on a duplicate start.
    /// </summary>
    internal static bool IsAlreadyRunning(string instanceKey)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // The watchdog creates the mutex under its documented full name
                // (SingleInstanceLock.Acquire) — probing the bare key would never match it.
                using var probe = Mutex.OpenExisting(BuildMutexName(instanceKey));
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false; // No such mutex → watchdog not running.
            }
            catch (Exception ex)
            {
                // Abandoned mutex / access denied: the mutex exists either way → treat as running.
                GeneralTracer.Info($"BowlLauncher: instance probe inconclusive ({ex.GetType().Name}) — treating as running.");
                return true;
            }
        }

        try
        {
            var lockPath = GetUnixLockPath(instanceKey);
            if (!File.Exists(lockPath)) return false;
            using var stream = File.Open(lockPath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false; // Exclusive open succeeded → the lock is stale (nobody holds it).
        }
        catch (IOException)
        {
            return true; // Held by a live watchdog.
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Deterministic single-instance key shared with the watchdog: SHA-256 over a versioned
    ///     prefix + target name + install path, hex-truncated to 32 chars. The install path is
    ///     case-folded on Windows only (its file system is case-insensitive; Unix is not).
    /// </summary>
    /// <remarks>
    ///     This is one half of the Core ↔ Bowl single-instance contract (the watchdog console
    ///     implements the identical algorithm); it is public so hosts can probe/report it without
    ///     reflection. The watchdog's mutex is named <c>Global\GeneralUpdate.Bowl.{key}</c>.
    /// </remarks>
    public static string BuildInstanceKey(string targetProcessName, string installPath)
    {
        var ignoreCase = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var normalizedPath = Path.GetFullPath(installPath).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (ignoreCase) normalizedPath = normalizedPath.ToLowerInvariant();
        var target = ignoreCase ? targetProcessName.Trim().ToLowerInvariant() : targetProcessName.Trim();

        var payload = $"GeneralUpdate.Bowl.v2|{target}|{normalizedPath}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            hex.Append(b.ToString("x2"));
        return hex.ToString(0, 32);
    }

    /// <summary>Well-known Unix lock file location (mirrored by the watchdog; temp dir = same user session).</summary>
    public static string GetUnixLockPath(string instanceKey) =>
        Path.Combine(Path.GetTempPath(), "GeneralUpdate", "bowl-locks", $"{instanceKey}.lock");

    /// <summary>
    ///     Full mutex name the watchdog registers for a target
    ///     (<c>Global\GeneralUpdate.Bowl.{key}</c>) — byte-identical to the watchdog console's
    ///     <c>SingleInstanceLock.Acquire</c>. One half of the probe contract: the launcher only
    ///     ever OPENS this name; the watchdog is its creator.
    /// </summary>
    internal static string BuildMutexName(string instanceKey) =>
        $"Global\\GeneralUpdate.Bowl.{instanceKey}";
}
