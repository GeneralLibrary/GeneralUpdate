using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Executes registered rules sequentially and aggregates their verdicts.
/// </summary>
/// <remarks>
///     <para>
///         A rule that throws is recorded as a failed rule (detail = exception message) and does
///         NOT interrupt the remaining rules — mirroring the safe-hook convention used across the
///         update pipeline. A session passes only when every rule passes.
///     </para>
/// </remarks>
public sealed class SelfCheckRunner
{
    private readonly IReadOnlyList<ISelfCheckRule> _rules;

    /// <summary>
    ///     Creates a runner over the given rules. Rules execute in enumeration order; callers
    ///     put the decisive built-in rule (process liveness) first so a dead process short-circuits
    ///     the expensive custom checks.
    /// </summary>
    public SelfCheckRunner(IEnumerable<ISelfCheckRule> rules)
    {
        _rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>
    ///     Runs every rule and returns the aggregated report. Never throws for rule failures;
    ///     only <paramref name="ct"/> cancellation propagates.
    /// </summary>
    public async Task<SelfCheckReport> RunAsync(SelfCheckContext ctx, CancellationToken ct)
    {
        var results = new List<SelfCheckRuleResult>(_rules.Count);
        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                results.Add(await rule.CheckAsync(ctx, ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"SelfCheckRunner: rule '{rule.Name}' threw: {ex.Message}");
                results.Add(new SelfCheckRuleResult(rule.Name, false, $"rule threw: {ex.Message}"));
            }
        }

        var report = new SelfCheckReport(results.All(r => r.Passed), results);
        GeneralTracer.Info($"SelfCheckRunner: session finished. Passed={report.Passed}, Rules={results.Count}.");
        return report;
    }
}
