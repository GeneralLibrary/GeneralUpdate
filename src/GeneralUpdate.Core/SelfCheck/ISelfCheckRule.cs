using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Context for one self-check session — every input a rule may need to render a verdict.
/// </summary>
/// <remarks>
///     <para>
///         The updater builds the context right after restarting the updated main application;
///         host-registered rules receive the same context as the built-in ones, so custom health
///         checks (database connectivity, licence state, ...) stay ignorant of the update pipeline.
///     </para>
///     <para>
///         <see cref="VerifyMarkerPath"/> additionally exposes the verify marker file location so
///         rules can cooperate on the file-based acknowledgement protocol (verify-ok / verify-fail).
///     </para>
/// </remarks>
public sealed record SelfCheckContext(
    string InstallPath,
    string AppName,
    string? TargetVersion,
    string? PreviousVersion,
    string? BackupDirectory,
    AppType Role,
    string? VerifyMarkerPath);

/// <summary>
///     Verdict of a single self-check rule.
/// </summary>
/// <param name="RuleName">The rule that produced the verdict (see <see cref="ISelfCheckRule.Name"/>).</param>
/// <param name="Passed">Whether the rule considers the application usable.</param>
/// <param name="Detail">
///     Human-readable evidence for the verdict (exit code, missing file, failure reason...).
///     Recorded verbatim in the self-check report for troubleshooting.
/// </param>
public sealed record SelfCheckRuleResult(string RuleName, bool Passed, string? Detail);

/// <summary>
///     Aggregate of every rule result in one self-check session.
/// </summary>
public sealed record SelfCheckReport(bool Passed, IReadOnlyList<SelfCheckRuleResult> Results)
{
    /// <summary>
    ///     Detail of the first failing rule, or <c>null</c> when the report passed.
    ///     Used as the rollback reason.
    /// </summary>
    public string? FirstFailureReason => Results.FirstOrDefault(r => !r.Passed)?.Detail;
}

/// <summary>
///     Extension point for custom self-check rules. Hosts implement this interface and register
///     it via <c>AbstractBootstrap.SelfCheckRule&lt;T&gt;()</c>; rules run sequentially inside the
///     updater's observation window and a thrown exception counts as a failed rule (never aborts
///     the remaining rules) — same safety convention as the update hooks.
/// </summary>
public interface ISelfCheckRule
{
    /// <summary>Rule name surfaced in <see cref="SelfCheckRuleResult.RuleName"/>.</summary>
    string Name { get; }

    /// <summary>
    ///     Executes the check. Implementations must be resilient: treat cancellations as
    ///     "not available" by returning a failed result or letting the exception bubble up
    ///     (the runner converts it into a failed result either way).
    /// </summary>
    Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct);
}
