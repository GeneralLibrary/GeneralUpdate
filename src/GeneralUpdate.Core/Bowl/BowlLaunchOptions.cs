using System;

namespace GeneralUpdate.Core.Bowl;

/// <summary>Dump capture level requested from the Bowl watchdog.</summary>
/// <remarks>
///     Mirrors the Bowl CLI values (<c>--dump-type Mini|Heap|Full</c>). Declared here — not
///     referenced from the Bowl assembly — so Core stays decoupled from the watchdog binary;
///     the CLI contract is the only coupling surface.
/// </remarks>
public enum BowlDumpType
{
    /// <summary>Mini dump (default — small, enough for managed stack diagnosis).</summary>
    Mini,

    /// <summary>Heap dump (includes the managed heap).</summary>
    Heap,

    /// <summary>Full dump (≈ process memory size; must be requested explicitly).</summary>
    Full
}

/// <summary>
///     Parameters for launching the independently installed Bowl watchdog
///     (see docs/bowl-redesign.md §2.2 — the CLI contract is the only coupling
///     between Core and Bowl).
/// </summary>
public sealed class BowlLaunchOptions
{
    /// <summary>
    ///     Full path of the independently installed watchdog executable (e.g.
    ///     <c>{ProgramData}/GeneralUpdate/bowl/bowl.exe</c>). Required — when missing,
    ///     <see cref="BowlLauncher.EnsureRunningAsync"/> logs and returns <c>false</c>;
    ///     everything else keeps working (Bowl is optional).
    /// </summary>
    public string BowlExecutablePath { get; set; } = string.Empty;

    /// <summary>
    ///     Watched process name (no extension). Half of the single-instance key; defaults to
    ///     <see cref="Configuration.UpdateConfiguration.MainAppName"/> when not set explicitly.
    /// </summary>
    public string? TargetProcessName { get; set; }

    /// <summary>
    ///     Installation root of the watched application (outbox root = <c>{install}/fail/</c>).
    ///     Other half of the single-instance key.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>Version of the watched application (evidence sub-directory name + report field).</summary>
    public string? Version { get; set; }

    /// <summary>Dump capture level. Default <see cref="BowlDumpType.Mini"/>.</summary>
    public BowlDumpType DumpType { get; set; } = BowlDumpType.Mini;

    /// <summary>
    ///     How long the watchdog waits for the target to appear before exiting on its own
    ///     (anti-orphan guard for boot-autostart without a running application). Default 30 minutes.
    /// </summary>
    public TimeSpan IdleExit { get; set; } = TimeSpan.FromMinutes(30);
}
