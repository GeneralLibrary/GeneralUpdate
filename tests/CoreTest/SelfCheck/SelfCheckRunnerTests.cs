using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.SelfCheck;

/// <summary>
///     SelfCheckRunner tests: aggregation semantics, a throwing rule does not interrupt the
///     remaining rules, and FirstFailureReason reflects the first failing rule.
/// </summary>
public sealed class SelfCheckRunnerTests
{
    private sealed class StaticRule(string name, bool passed, string? detail = null) : ISelfCheckRule
    {
        public string Name { get; } = name;

        public Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct)
            => Task.FromResult(new SelfCheckRuleResult(Name, passed, detail ?? $"{Name}-detail"));
    }

    private sealed class ThrowingRule(string name) : ISelfCheckRule
    {
        public string Name { get; } = name;

        public Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private static readonly SelfCheckContext Ctx = new(
        Path.GetTempPath(), "app.exe", "1.0.2", "1.0.1", null,
        GeneralUpdate.Core.Configuration.AppType.Upgrade, null);

    [Fact]
    public async Task RunAsync_AllRulesPass_Aggregates()
    {
        var runner = new SelfCheckRunner(new ISelfCheckRule[]
        {
            new StaticRule("r1", true),
            new StaticRule("r2", true),
        });

        var report = await runner.RunAsync(Ctx, CancellationToken.None);

        Assert.True(report.Passed);
        Assert.Equal(2, report.Results.Count);
        Assert.Null(report.FirstFailureReason);
    }

    [Fact]
    public async Task RunAsync_SingleFailure_FailsSessionAndCarriesFirstReason()
    {
        var runner = new SelfCheckRunner(new ISelfCheckRule[]
        {
            new StaticRule("ok-rule", true),
            new StaticRule("bad-rule", false, "specific reason"),
            new StaticRule("later-rule", false, "second failure"),
        });

        var report = await runner.RunAsync(Ctx, CancellationToken.None);

        Assert.False(report.Passed);
        Assert.Equal(3, report.Results.Count);
        Assert.Equal("specific reason", report.FirstFailureReason);
    }

    [Fact]
    public async Task RunAsync_ThrowingRule_BecomesFailedRuleWithoutInterrupting()
    {
        var runner = new SelfCheckRunner(new ISelfCheckRule[]
        {
            new StaticRule("before", true),
            new ThrowingRule("explodes"),
            new StaticRule("after", true),
        });

        var report = await runner.RunAsync(Ctx, CancellationToken.None);

        // "after" still ran — one broken rule never hides the other verdicts.
        Assert.Equal(3, report.Results.Count);
        Assert.False(report.Passed);
        Assert.False(report.Results.Single(r => r.RuleName == "explodes").Passed);
        Assert.True(report.Results.Single(r => r.RuleName == "after").Passed);
        Assert.Contains("boom", report.FirstFailureReason);
    }

    [Fact]
    public async Task RunAsync_RulesRunInRegistrationOrder()
    {
        var runner = new SelfCheckRunner(new ISelfCheckRule[]
        {
            new StaticRule("first", true),
            new StaticRule("second", true),
        });

        var report = await runner.RunAsync(Ctx, CancellationToken.None);

        Assert.Equal("first", report.Results[0].RuleName);
        Assert.Equal("second", report.Results[1].RuleName);
    }
}
