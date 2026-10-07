namespace GeneralUpdate.Core.Configuration;

/// <summary>Opt-in integration with the separately deployed Bowl v1 host.</summary>
public sealed class BowlOptions
{
    public bool Enabled { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public int ReadyTimeoutSeconds { get; set; } = 10;
    /// <summary>Observe process survival, not business health, after launch.</summary>
    public bool VerifyLaunch { get; set; } = true;
    public int HealthTimeoutSeconds { get; set; } = 5;
    public int UpdateTimeoutSeconds { get; set; } = 600;
    public bool AutoRollback { get; set; }
    /// <summary>Name of a host-provisioned environment variable; never the credential itself.</summary>
    public string? CredentialEnvironmentVariable { get; set; }
}
