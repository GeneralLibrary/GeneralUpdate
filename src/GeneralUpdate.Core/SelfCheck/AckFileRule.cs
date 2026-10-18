using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Application-layer acknowledgement rule (strict mode, RequireAck).
/// </summary>
/// <remarks>
///     <para>
///         Only the business itself knows what "usable" means (database reachable, licence valid,
///         core services ready). The file protocol is the cooperation contract: after the updater
///         writes the verify marker, the restarted main application detects it early
///         (via <see cref="UpdateSelfCheck.GetPendingVerification"/>), runs its own health checks
///         and reports the outcome with <see cref="UpdateSelfCheck.ReportHealth"/> — a
///         <c>generalupdate.verify.ok</c> or <c>generalupdate.verify.fail</c> file next to the marker.
///     </para>
///     <para>
///         This rule evaluates the current acknowledgement state at a point in time. The windowed
///         observation (waiting for the ack inside <see cref="ProcessLaunchRule"/>) already covers
///         the updater's verdict; this rule exists for hosts that manage the process themselves and
///         want the file protocol as an explicit, reportable rule entry. It shares the same
///         <see cref="UpdateVerificationMarker.TryReadAck"/> reader as the windowed path, so both
///         can never disagree about the same files.
///     </para>
/// </remarks>
public sealed class AckFileRule : ISelfCheckRule
{
    /// <inheritdoc />
    public string Name => "AckFile";

    /// <inheritdoc />
    public Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct)
    {
        var ack = UpdateVerificationMarker.TryReadAck(ctx.InstallPath, out var reason);
        return Task.FromResult(ack switch
        {
            VerificationAckState.Ok =>
                new SelfCheckRuleResult(Name, true, "application acknowledged healthy (verify-ok)"),
            VerificationAckState.Fail =>
                new SelfCheckRuleResult(Name, false, $"application reported unhealthy: {reason}"),
            _ => new SelfCheckRuleResult(Name, false,
                "no acknowledgement file present (application did not confirm health)")
        });
    }
}
