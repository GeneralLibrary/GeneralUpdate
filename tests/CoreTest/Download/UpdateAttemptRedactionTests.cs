using CoreTest.TestSupport;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;

namespace CoreTest.Download;

/// <summary>
///     Shared credential redaction used by server-facing evidence built outside the update
///     journal: the self-check verdict reason crosses the wire twice — verbatim in the
///     Startup report's Error.Message and (via RollbackRequest.Reason) in the Rollback
///     report and the rollback diagnostics bundle. A custom rule exception or a
///     verify-fail acknowledgement can embed a configured token, app secret or basic
///     password, so the redaction must be applied BEFORE the reason leaves the strategy.
/// </summary>
public sealed class UpdateAttemptRedactionTests
{
    [Fact]
    public void Redact_StripsConfiguredCredentials()
    {
        var token = FixtureValues.Token("redact");
        var secret = FixtureValues.Secret("redact");
        var password = FixtureValues.Secret("redact-pw");
        var config = new UpdateContext
        {
            Token = token,
            AppSecretKey = secret,
            BasicPassword = password
        };

        var redacted = UpdateAttempt.Redact(
            $"verify failed: {token} / {secret} / {password} / safe-part", config);

        Assert.Equal("verify failed: [REDACTED] / [REDACTED] / [REDACTED] / safe-part", redacted);
    }

    [Fact]
    public void Redact_NoConfiguredSecrets_ValueUnchanged()
    {
        var redacted = UpdateAttempt.Redact("plain failure reason", new UpdateContext());

        Assert.Equal("plain failure reason", redacted);
    }

    [Fact]
    public void Redact_NullValue_ReturnsNull()
    {
        Assert.Null(UpdateAttempt.Redact(null, new UpdateContext { Token = FixtureValues.Token("redact-null") }));
    }
}
